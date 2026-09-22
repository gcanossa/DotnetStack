namespace GKit.UI;

/// <summary>
/// A start/end pair of times of day, either end of which may be unset.
/// </summary>
/// <remarks>
/// Lives here rather than nested in a picker component so both adapters bind to the same type.
/// </remarks>
public class TimeRange
{
  public TimeSpan? Start { get; set; }
  public TimeSpan? End { get; set; }
}
