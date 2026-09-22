using Microsoft.JSInterop;

namespace GKit.BlazorExt;

public abstract class DocumentEventSourceBase : IAsyncDisposable
{
  protected readonly IJSObjectReference _module;

  /// <summary>
  /// The reference handed to <c>connect</c>. <c>disconnect</c> must receive the same object so
  /// the JS side can find and remove the listener it registered.
  /// </summary>
  internal DotNetObjectReference<DocumentEventSourceBase>? SelfReference { get; set; }

  public DocumentEventSourceBase(IJSObjectReference module)
  {
    _module = module;
  }

  private bool _disposed = false;

  public async ValueTask DisposeAsync()
  {
    if (_disposed) return;
    _disposed = true;

    try
    {
      // Previously passed `this`, which the interop layer serialises as JSON: the JS side could
      // not match it to the registered listener, so every navigation leaked a document-level
      // event handler plus the .NET object reference.
      await _module.InvokeVoidAsync("disconnect", SelfReference);
    }
    catch (JSDisconnectedException)
    {
      // The circuit is already gone; nothing to detach.
    }
    catch (ObjectDisposedException)
    {
    }
    finally
    {
      SelfReference?.Dispose();
      SelfReference = null;
    }

    GC.SuppressFinalize(this);
  }
}
