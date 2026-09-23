using GKit.Quartz;
using Microsoft.Extensions.Logging;
using Quartz;

namespace GKIT-NAMESPACE;

/// <summary>
/// UseGKitQuartz scans the entry assembly for job types, so this needs no registration. The
/// schedule can also be overridden per environment from GKit:Quartz:Schedules, and the job
/// disabled entirely through GKit:Quartz:DisabledJobs.
/// </summary>
//#if (kind_cron)
[CronSchedule("GKIT-SCHEDULE")]
//#endif
//#if (kind_interval)
[TimeSpanSchedule("GKIT-INTERVAL")]
//#endif
public class SampleJob(ILogger<SampleJob> logger) : IJob
{
  public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
  {
    logger.LogInformation("SampleJob running at {Now}", context.FireTimeUtc);

    await Task.CompletedTask;
  }
}
