namespace GKit.Quartz;

public class GKitQuartzOptions
{
    public List<ScheduleOptions> Schedules { get; set; } = [];
    
    public List<string>? DisabledJobs { get; set; }
    public List<string>? EnabledJobs { get; set; }
    
    public class ScheduleOptions
    {
        public required string JobTypeName { get; set; }

        /// <summary>Repeat forever, every <c>Interval</c>.</summary>
        public TimeSpan? Interval { get; set; }

        /// <summary>Run once a day at this time of day.</summary>
        public TimeSpan? DailyAt { get; set; }

        public string? CronExpression { get; set; }

        internal int SpecifiedCount =>
            (Interval is not null ? 1 : 0) + (DailyAt is not null ? 1 : 0) + (CronExpression is not null ? 1 : 0);
    }
}