namespace GC.Blazor;

public class TimerService
{
  public IDisposable SetTimeout(Action callback, TimeSpan timeout)
  {
    var timer = new Timer(p => callback?.Invoke(), null, (int)timeout.TotalMilliseconds, Timeout.Infinite);

    return new DisposableHandle((IDisposable)timer);
  }
  public IDisposable SetInterval(Action callback, TimeSpan timeout)
  {
    var timer = new Timer(p => callback?.Invoke(), null, (int)timeout.TotalMilliseconds, (int)timeout.TotalMilliseconds);

    return new DisposableHandle((IDisposable)timer);
  }
}
