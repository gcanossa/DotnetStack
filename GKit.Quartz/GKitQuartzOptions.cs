namespace GKit.Quartz;

public class GKitQuartzOptions
{
    public List<ScheduleOptions> Schedules { get; set; } = [];
    
    public List<string>? DisabledJobs { get; set; }
    public List<string>? EnabledJobs { get; set; }
    
    public class ScheduleOptions
    {
        public required string JobTypeName { get; set; }
        public TimeSpan? Interval { get; set; }
        public string? CronExpression { get; set; }
    }
}