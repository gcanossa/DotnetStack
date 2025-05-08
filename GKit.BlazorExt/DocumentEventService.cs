using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace GKit.BlazorExt;

public class DocumentEventSource : IAsyncDisposable
{
  private readonly IJSObjectReference _module;

  [DynamicDependency(nameof(KeyUp))]
  [DynamicDependency(nameof(KeyDown))]
  [DynamicDependency(nameof(KeyPress))]
  public DocumentEventSource(IJSObjectReference module)
  {
    _module = module;
  }

  public event EventHandler<KeyboardEventArgs>? KeyUp;
  public event EventHandler<KeyboardEventArgs>? KeyDown;
  public event EventHandler<KeyboardEventArgs>? KeyPress;

  [JSInvokable]
  public Task OnKeyUp(KeyboardEventArgs args)
  {
    KeyUp?.Invoke(this, args);

    return Task.CompletedTask;
  }

  [JSInvokable]
  public Task OnKeyDown(KeyboardEventArgs args)
  {
    KeyDown?.Invoke(this, args);

    return Task.CompletedTask;
  }

  [JSInvokable]
  public Task OnKeyPress(KeyboardEventArgs args)
  {
    KeyPress?.Invoke(this, args);

    return Task.CompletedTask;
  }

  private bool _disposed = false;
  public async ValueTask DisposeAsync()
  {
    if (!_disposed)
    {
      try
      {
        await _module.InvokeVoidAsync("disconnect", this);
      }
      catch { }
      await _module.DisposeAsync();
      _disposed = true;
    }
  }
}

public class DocumentEventService : IAsyncDisposable
{
  private readonly Lazy<Task<IJSObjectReference>> moduleTask;

  public DocumentEventService(IJSRuntime jsRuntime)
  {
    moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
      "import", $"./_content/GKit.BlazorExt/{nameof(DocumentEventService)}.js").AsTask());
  }


  public async ValueTask<DocumentEventSource> Connect()
  {
    var module = await moduleTask.Value;

    var source = new DocumentEventSource(module);
    var objRef = DotNetObjectReference.Create(source);

    await module.InvokeVoidAsync("connect", objRef);
    return source;
  }

  private bool _disposed = false;
  public async ValueTask DisposeAsync()
  {
    if (!_disposed && moduleTask.IsValueCreated)
    {
      var module = await moduleTask.Value;
      await module.DisposeAsync();
      _disposed = true;
    }
  }
}
