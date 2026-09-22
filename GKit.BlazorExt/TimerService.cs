using Microsoft.Extensions.Logging;

namespace GKit.BlazorExt;

/// <summary>
/// setTimeout / setInterval for Blazor components.
/// <para>
/// The returned <see cref="IDisposable"/> owns the timer: callers must dispose it, and
/// <c>TimedScope</c> does. Timers are not bound to the DI scope that produced this service.
/// </para>
/// </summary>
public class TimerService(ILogger<TimerService>? logger = null)
{
  /// <summary>
  /// Async callbacks previously discarded the returned <see cref="Task"/>, so a faulting
  /// callback became an unobserved exception that reported nothing and stopped nothing.
  /// </summary>
  private TimerCallback Wrap(Func<Task> callback) => async _ =>
  {
    try
    {
      await callback().ConfigureAwait(false);
    }
    catch (Exception e)
    {
      logger?.LogError(e, "Unhandled exception in a {Service} callback", nameof(TimerService));
      OnCallbackError?.Invoke(e);
    }
  };

  private TimerCallback Wrap(Action callback) => _ =>
  {
    try
    {
      callback();
    }
    catch (Exception e)
    {
      logger?.LogError(e, "Unhandled exception in a {Service} callback", nameof(TimerService));
      OnCallbackError?.Invoke(e);
    }
  };

  /// <summary>Raised when a scheduled callback throws. Primarily a test and diagnostics hook.</summary>
  public event Action<Exception>? OnCallbackError;

  // The TimeSpan overloads of Timer are used throughout: the previous (int) casts of
  // TotalMilliseconds overflow for any period beyond ~24.8 days.
  public IDisposable SetTimeout(Action callback, TimeSpan timeout)
    => new Timer(Wrap(callback), null, timeout, Timeout.InfiniteTimeSpan);

  public IDisposable SetTimeoutAsync(Func<Task> callback, TimeSpan timeout)
    => new Timer(Wrap(callback), null, timeout, Timeout.InfiniteTimeSpan);

  public IDisposable SetInterval(Action callback, TimeSpan period)
    => new Timer(Wrap(callback), null, period, period);

  public IDisposable SetIntervalAsync(Func<Task> callback, TimeSpan period)
    => new Timer(Wrap(callback), null, period, period);
}
