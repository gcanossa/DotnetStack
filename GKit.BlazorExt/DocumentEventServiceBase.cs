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

    // Held on the source so DisposeAsync can hand the *same* reference to disconnect and then
    // dispose it; it used to be created here and never released.
    var objRef = DotNetObjectReference.Create<DocumentEventSourceBase>(source);
    source.SelfReference = objRef;

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