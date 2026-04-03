namespace GKit.BlazorExt;

public class TimerService
{
  public IDisposable SetTimeout(Action callback, TimeSpan timeout)
  {
    var timer = new Timer(p => callback?.Invoke(), null, (int)timeout.TotalMilliseconds, Timeout.Infinite);

    return timer;
  }
  
  public IDisposable SetTimeoutAsync(Func<Task> callback, TimeSpan timeout)
  {
    var timer = new Timer(p => callback?.Invoke(), null, (int)timeout.TotalMilliseconds, Timeout.Infinite);

    return timer;
  }
  
  public IDisposable SetInterval(Action callback, TimeSpan timeout)
  {
    var timer = new Timer(p => callback?.Invoke(), null, (int)timeout.TotalMilliseconds, (int)timeout.TotalMilliseconds);

    return timer;
  }
  
  public IDisposable SetIntervalAsync(Func<Task> callback, TimeSpan timeout)
  {
    var timer = new Timer(p => callback?.Invoke(), null, (int)timeout.TotalMilliseconds, (int)timeout.TotalMilliseconds);

    return timer;
  }
}
