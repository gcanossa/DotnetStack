namespace GKit.Quartz;

[AttributeUsage(AttributeTargets.Class)]
public class TimeSpanScheduleAttribute(TimeSpan value) : Attribute
{
    public TimeSpan Value { get; } = value;
}