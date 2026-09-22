namespace GKit.Quartz;

/// <summary>Runs the job repeatedly, every <see cref="Value"/>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class IntervalScheduleAttribute(TimeSpan value) : Attribute
{
    public TimeSpan Value { get; } = value;

    public IntervalScheduleAttribute(string value) : this(TimeSpan.Parse(value)) { }
}

/// <summary>Runs the job once a day, at the time of day given by <see cref="Value"/>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DailyAtScheduleAttribute(TimeSpan value) : Attribute
{
    public TimeSpan Value { get; } = value;

    public DailyAtScheduleAttribute(string value) : this(TimeSpan.Parse(value)) { }
}

/// <summary>
/// Runs the job once a day, at the hour and minute of its configured time of day.
/// </summary>
/// <remarks>
/// The name says "interval" but the behaviour was always daily-at: the implementation used
/// <c>CronScheduleBuilder.DailyAtHourAndMinute</c>, so
/// <c>[TimeSpanSchedule(TimeSpan.FromMinutes(15))]</c> ran at 00:15 once a day rather than
/// every quarter hour. The behaviour is preserved here so existing jobs keep their schedule;
/// use <see cref="IntervalScheduleAttribute"/> for a repeating interval or
/// <see cref="DailyAtScheduleAttribute"/> to say daily-at explicitly.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
[Obsolete("Ambiguous name. Use DailyAtScheduleAttribute for the same behaviour, " +
          "or IntervalScheduleAttribute for a repeating interval.")]
public class TimeSpanScheduleAttribute(TimeSpan value) : DailyAtScheduleAttribute(value);
