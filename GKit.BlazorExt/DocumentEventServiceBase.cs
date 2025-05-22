using Microsoft.JSInterop;

namespace GKit.BlazorExt;

public abstract class DocumentEventServiceBase<T> : IAsyncDisposable where T : DocumentEventSourceBase
{
  protected readonly Lazy<Task<IJSObjectReference>> moduleTask;

  public DocumentEventServiceBase(IJSRuntime jsRuntime)
  {
    moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
      "import", $"./_content/GKit.BlazorExt/{GetType().Name}.js").AsTask());
  }

  protected abstract T CreateEventSource(IJSObjectReference module);

  public async ValueTask<T> Connect()
  {
    var module = await moduleTask.Value;

    var source = CreateEventSource(module);
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