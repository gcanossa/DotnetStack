using Microsoft.JSInterop;

namespace GKit.SmartCardHost.Blazor;

public delegate Task SmartCardHostServiceEvent();
public delegate Task SmartCardHostServiceCardAvailableEvent(string cardId);
public delegate Task SmartCardHostServiceReadersChangedEvent(string[] readers);

public class SmartCardHostServiceHandle
{
  public bool IsBridgeAvailable { get; protected set; } = false;

  public bool IsReaderAvailable => AvailableReaders.Length > 0;

  public bool IsActive => IsBridgeAvailable && IsReaderAvailable;

  public string[] AvailableReaders { get; protected set; } = [];

  public event SmartCardHostServiceEvent? Connected;
  public event SmartCardHostServiceEvent? Disconnected;
  public event SmartCardHostServiceCardAvailableEvent? CardAvailable;
  public event SmartCardHostServiceReadersChangedEvent? ReadersChanged;

  [JSInvokable]
  public async Task OnConnected()
  {
    await (Connected?.Invoke() ?? Task.CompletedTask);
    IsBridgeAvailable = true;
  }
  [JSInvokable]
  public async Task OnDisconnected()
  {
    await (Disconnected?.Invoke() ?? Task.CompletedTask);
    IsBridgeAvailable = false;
    AvailableReaders = [];
  }
  [JSInvokable]
  public async Task OnCardAvailable(string cardId)
  {
    await (CardAvailable?.Invoke(cardId) ?? Task.CompletedTask);
  }
  [JSInvokable]
  public async Task OnReadersChanged(string[] readers)
  {
    AvailableReaders = readers;
    await (ReadersChanged?.Invoke(readers) ?? Task.CompletedTask);
  }
}

public class SmartCardHostService(IJSRuntime jsRuntime, SmartCardHostServiceHandle handle)
{
  protected readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
    "import", $"./_content/GKit.SmartCardHost.Blazor/{nameof(SmartCardHostService)}.js").AsTask());
  
  private DotNetObjectReference<SmartCardHostServiceHandle>? objRef;
  
  public async Task<bool> ConnectAsync()
  {
    var module = await moduleTask.Value;
    
    objRef ??= DotNetObjectReference.Create(handle);
    
    var state = await module.InvokeAsync<string>("connectCardReaderService", objRef);
    if (state == "Connected")
      await handle.OnConnected();
    else
      await handle.OnDisconnected();
      
    return state == "Connected";
  }

  public async ValueTask DisconnectAsync()
  {
    var module = await moduleTask.Value;
    
    if (objRef is not null)
      await module.InvokeAsync<string>("disconnectCardReaderService", objRef);
    objRef?.Dispose();
    objRef = null;
  }
}