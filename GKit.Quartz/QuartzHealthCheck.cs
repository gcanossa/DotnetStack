using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Quartz;

namespace GKit.Quartz;

public static class QuartzHealthCheckExtensions
{
  internal static async Task ScheduleQuartzCheck(IScheduler scheduler, IOptions<QuartzHealthCheckOptions> options)
  {
    var job = JobBuilder.Create<QuartzProbeJob>()
      .WithIdentity("quartz.check", "healthchecks")
      .Build();

    var trigger = TriggerBuilder.Create()
      .WithIdentity("quartz.check", "healthchecks")
      .StartNow()
      .WithSimpleSchedule(x => x
        .WithIntervalInSeconds((int)options.Value.HeartBeat.TotalSeconds)
        .RepeatForever())
      .Build();

    await scheduler.ScheduleJob(job, trigger);
  }

  public static IHealthChecksBuilder AddQuartzCheck(this IHealthChecksBuilder ext, string name)
  {
    ext.Services.AddSingleton<QuartzProbe>();
    var builder = ext.AddCheck<QuartzHealthCheck>(name);
    
    return builder;
  }
}

public class QuartzHealthCheckOptions
{
  public TimeSpan HeartBeat { get; set; } = TimeSpan.FromMinutes(1);
  public uint FailThreshold { get; set; } = 1;
}

public class QuartzHealthCheck(QuartzProbe probe, IOptions<QuartzHealthCheckOptions> options) : IHealthCheck
{
  public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
  {
    return Task.FromResult(probe.LostHeartBeats > options.Value.FailThreshold ? HealthCheckResult.Unhealthy() : HealthCheckResult.Healthy());
  }
}

public class QuartzProbe(IOptions<QuartzHealthCheckOptions> options)
{
  public uint LostHeartBeats => (uint)Math.Floor(
    (DateTimeOffset.UtcNow - LastUpdate).TotalMilliseconds / options.Value.HeartBeat.TotalMilliseconds);

  public DateTimeOffset LastUpdate { get; private set; } = DateTimeOffset.UtcNow;
  public void Update()
  {
    LastUpdate = DateTimeOffset.UtcNow;
  }
}

public class QuartzProbeJob(QuartzProbe probe) : IJob
{
  public Task Execute(IJobExecutionContext context)
  {
    probe.Update();

    return Task.CompletedTask;
  }
}