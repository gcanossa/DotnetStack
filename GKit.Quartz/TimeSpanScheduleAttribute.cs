namespace GKit.Quartz;

[AttributeUsage(AttributeTargets.Class)]
public class TimeSpanScheduleAttribute : Attribute
{
    public TimeSpanScheduleAttribute(TimeSpan value)
    {
        Value = value;
    }

    public TimeSpan Value { get; set; }
}