namespace GC.Blazor;

public static class WebApi
{
  public static IDisposable SetTimeout(Action callback, TimeSpan timeout)
  {
    var timer = new Timer(p => callback?.Invoke(), null, (int)timeout.TotalMicroseconds, Timeout.Infinite);

    return new DisposableHandle((IDisposable)timer);
  }
  public static IDisposable SetInterval(Action callback, TimeSpan timeout)
  {
    var timer = new Timer(p => callback?.Invoke(), null, (int)timeout.TotalMicroseconds, (int)timeout.TotalMicroseconds);

    return new DisposableHandle((IDisposable)timer);
  }
}
