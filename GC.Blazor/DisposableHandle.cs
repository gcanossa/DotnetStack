
namespace GC.Blazor;

public class DisposableHandle : IAsyncDisposable, IDisposable
{
  private readonly IDisposable? _disposable;
  private readonly IAsyncDisposable? _asyncDisposable;

  public DisposableHandle(IDisposable item)
  {
    _disposable = item;
  }
  public DisposableHandle(IAsyncDisposable item)
  {
    _asyncDisposable = item;
  }

  public void Dispose()
  {
    System.GC.SuppressFinalize(this);

    _disposable?.Dispose();
  }

  public async ValueTask DisposeAsync()
  {
    Dispose();

    if(_asyncDisposable != null) await _asyncDisposable.DisposeAsync();
  }
}
