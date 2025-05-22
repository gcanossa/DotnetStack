using Microsoft.JSInterop;

namespace GKit.BlazorExt;

public abstract class DocumentEventSourceBase : IAsyncDisposable
{
  protected readonly IJSObjectReference _module;

  public DocumentEventSourceBase(IJSObjectReference module)
  {
    _module = module;
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