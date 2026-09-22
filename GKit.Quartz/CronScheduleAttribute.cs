namespace GKit.Quartz;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class CronScheduleAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}