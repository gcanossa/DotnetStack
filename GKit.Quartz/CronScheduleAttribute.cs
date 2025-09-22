namespace GKit.Quartz;

[AttributeUsage(AttributeTargets.Class)]
public class CronScheduleAttribute : Attribute
{
    public CronScheduleAttribute(string value)
    {
        Value = value;
    }

    public string Value { get; set; }
}