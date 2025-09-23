namespace GKit.Quartz;

[AttributeUsage(AttributeTargets.Class)]
public class CronScheduleAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}