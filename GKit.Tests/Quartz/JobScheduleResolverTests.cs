using System.Reflection;
using GKit.Quartz;
using Quartz;
using Quartz.Impl.Triggers;

namespace GKit.Tests.Quartz;

public class JobScheduleResolverTests
{
  private sealed class NoScheduleJob : IJob
  {
    public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
  }

  [CronSchedule("0/20 * * ? * MON-SUN *")]
  private sealed class CronJob : IJob
  {
    public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
  }

  [IntervalSchedule("00:15:00")]
  private sealed class IntervalJob : IJob
  {
    public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
  }

  [DailyAtSchedule("00:15:00")]
  private sealed class DailyJob : IJob
  {
    public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
  }

  [CronSchedule("0 0 1 * * ?")]
  [CronSchedule("0 0 13 * * ?")]
  private sealed class TwiceDailyJob : IJob
  {
    public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
  }

  private abstract class AbstractJob : IJob
  {
    public abstract Task Execute(IJobExecutionContext context);
  }

  private sealed class GenericJob<T> : IJob
  {
    public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
  }

  private static GKitQuartzOptions Options(
    List<string>? enabled = null, List<string>? disabled = null,
    List<GKitQuartzOptions.ScheduleOptions>? schedules = null) => new()
  {
    EnabledJobs = enabled,
    DisabledJobs = disabled,
    Schedules = schedules ?? []
  };

  // ---- enable / disable precedence

  [Fact]
  public void Everything_runs_when_neither_list_is_configured()
  {
    Assert.True(JobScheduleResolver.IsEnabled(typeof(CronJob), Options()));
  }

  [Fact]
  public void A_disabled_job_does_not_run()
  {
    var options = Options(disabled: [typeof(CronJob).FullName!]);

    Assert.False(JobScheduleResolver.IsEnabled(typeof(CronJob), options));
  }

  [Fact]
  public void An_allow_list_excludes_everything_not_on_it()
  {
    var options = Options(enabled: [typeof(CronJob).FullName!]);

    Assert.True(JobScheduleResolver.IsEnabled(typeof(CronJob), options));
    Assert.False(JobScheduleResolver.IsEnabled(typeof(IntervalJob), options));
  }

  [Fact]
  public void DisabledJobs_still_applies_when_EnabledJobs_is_also_set()
  {
    // `EnabledJobs?.Any(..) ?? true && !Disabled` parsed as `Enabled ?? (true && !Disabled)`
    // because && binds tighter than ??, so the deny-list was dead whenever an allow-list existed.
    var options = Options(
      enabled: [typeof(CronJob).FullName!, typeof(IntervalJob).FullName!],
      disabled: [typeof(IntervalJob).FullName!]);

    Assert.True(JobScheduleResolver.IsEnabled(typeof(CronJob), options));
    Assert.False(JobScheduleResolver.IsEnabled(typeof(IntervalJob), options));
  }

  // ---- schedule semantics

  [Fact]
  public void An_interval_schedule_repeats_at_that_interval()
  {
    // [TimeSpanSchedule(FromMinutes(15))] used to build DailyAtHourAndMinute(0, 15) — once a
    // day at 00:15, i.e. 1/96th of the intended frequency.
    var job = JobScheduleResolver.Resolve([typeof(IntervalJob)], Options()).Scheduled.Single();

    var trigger = Assert.IsAssignableFrom<ISimpleTrigger>(job.Triggers.Single());

    Assert.Equal(TimeSpan.FromMinutes(15), trigger.RepeatInterval);
    Assert.Equal(SimpleTriggerImpl.RepeatIndefinitely, trigger.RepeatCount);
  }

  [Fact]
  public void A_daily_at_schedule_fires_once_a_day()
  {
    var job = JobScheduleResolver.Resolve([typeof(DailyJob)], Options()).Scheduled.Single();

    var trigger = Assert.IsAssignableFrom<ICronTrigger>(job.Triggers.Single());

    Assert.Equal("0 15 0 ? * *", trigger.CronExpressionString);
  }

  [Fact]
  public void A_cron_schedule_uses_the_given_expression()
  {
    var job = JobScheduleResolver.Resolve([typeof(CronJob)], Options()).Scheduled.Single();

    var trigger = Assert.IsAssignableFrom<ICronTrigger>(job.Triggers.Single());

    Assert.Equal("0/20 * * ? * MON-SUN *", trigger.CronExpressionString);
  }

  [Fact]
  public void Multiple_schedule_attributes_produce_multiple_triggers()
  {
    var job = JobScheduleResolver.Resolve([typeof(TwiceDailyJob)], Options()).Scheduled.Single();

    Assert.Equal(2, job.Triggers.Count);
  }

  [Fact]
  public void A_schedule_from_configuration_is_honoured()
  {
    var options = Options(schedules:
    [
      new GKitQuartzOptions.ScheduleOptions
      {
        JobTypeName = typeof(NoScheduleJob).FullName!,
        Interval = TimeSpan.FromMinutes(5)
      }
    ]);

    var job = JobScheduleResolver.Resolve([typeof(NoScheduleJob)], options).Scheduled.Single();

    Assert.Equal(TimeSpan.FromMinutes(5),
      Assert.IsAssignableFrom<ISimpleTrigger>(job.Triggers.Single()).RepeatInterval);
  }

  [Fact]
  public void A_job_with_no_schedule_at_all_is_skipped()
  {
    var resolution = JobScheduleResolver.Resolve([typeof(NoScheduleJob)], Options());

    Assert.Empty(resolution.Scheduled);
    Assert.Equal("NO SCHEDULES", resolution.Skipped.Single().Reason);
  }

  // ---- identity

  [Fact]
  public void Job_keys_are_stable_across_restarts()
  {
    // A fresh Guid per start orphans a row per job per restart in a persistent job store and
    // stops ScheduleJobs(replace: true) from matching the previous registration.
    var first = JobScheduleResolver.Resolve([typeof(CronJob)], Options()).Scheduled.Single();
    var second = JobScheduleResolver.Resolve([typeof(CronJob)], Options()).Scheduled.Single();

    Assert.Equal(first.Detail.Key, second.Detail.Key);
  }

  [Fact]
  public void Trigger_keys_are_unique_across_jobs_with_the_same_type_name()
  {
    var resolution = JobScheduleResolver.Resolve([typeof(CronJob), typeof(TwiceDailyJob)], Options());

    var keys = resolution.Scheduled.SelectMany(j => j.Triggers).Select(t => t.Key).ToList();

    Assert.Equal(keys.Count, keys.Distinct().Count());
  }

  // ---- type discovery

  [Fact]
  public void Discovery_skips_interfaces_abstract_types_and_open_generics()
  {
    var found = JobScheduleResolver.FindJobTypes(typeof(JobScheduleResolverTests).Assembly).ToList();

    Assert.Contains(typeof(CronJob), found);
    Assert.DoesNotContain(typeof(IJob), found);
    Assert.DoesNotContain(typeof(AbstractJob), found);
    Assert.DoesNotContain(typeof(GenericJob<>), found);
  }

  [Fact]
  public void Discovery_of_an_unloadable_assembly_does_not_throw()
  {
    // Assembly.GetTypes() throws ReflectionTypeLoadException when a referenced assembly is
    // missing; startup must survive that rather than abort.
    var ex = Record.Exception(() =>
      JobScheduleResolver.FindJobTypes(typeof(object).Assembly).ToList());

    Assert.Null(ex);
  }

  [Fact]
  public void A_null_assembly_is_rejected()
  {
    Assert.Throws<ArgumentNullException>(() => JobScheduleResolver.FindJobTypes(null!).ToList());
  }
}
