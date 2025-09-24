﻿using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace GKit.Quartz;

public static class GKitQuartzExtensions
{
  public static IServiceCollection AddGKitQuartz(this IServiceCollection services, string? schedulerName = null)
  {
    
    services.AddQuartz(q =>
    {
      q.SchedulerName = schedulerName ?? Assembly.GetExecutingAssembly().FullName!;
    });
    services.AddQuartzHostedService(options =>
    {
      options.AwaitApplicationStarted = true;
      options.WaitForJobsToComplete = true;
    });
    
    return services;
  }
  
  public static async Task<IHost> UseGKitQuartz(this IHost host, string? schedulerName = null, Action<IScheduler>? config = null, params Assembly[] otherAssemblies)
  {
    using var scope = host.Services.CreateScope();
    var schedulerFactory = scope.ServiceProvider.GetRequiredService<ISchedulerFactory>();
    var scheduler = await schedulerFactory.GetScheduler(schedulerName ?? Assembly.GetExecutingAssembly().FullName!);

    var logger = scope.ServiceProvider.GetRequiredService<ILogger>();
    
    logger.LogInformation("Scanning {Assembly} for jobs", Assembly.GetExecutingAssembly().FullName);
    var jobTypes = FindConfiguredJobTypes(Assembly.GetExecutingAssembly()).ToList();
    logger.LogInformation("Found {Count}", jobTypes.Count);
    foreach (var assembly in otherAssemblies.Where(p => p != Assembly.GetExecutingAssembly()).Distinct())
    {
      logger.LogInformation("Scanning {Assembly} for jobs", assembly.FullName);
      var found = FindConfiguredJobTypes(assembly).ToList();
      jobTypes.AddRange(found);
      logger.LogInformation("Found {Count}", found.Count);
    }

    var jobConfigs = jobTypes
      .Select(type => new
      {
        Type = type,
        Schedules = type.GetCustomAttributes<CronScheduleAttribute>().Cast<object>()
          .Union(type.GetCustomAttributes<TimeSpanScheduleAttribute>()).ToList(),
        Detail = JobBuilder.Create().OfType(type).WithIdentity($"{type.Name}-{Guid.NewGuid():N}").Build()
      })
      .ToDictionary(
        k => k.Detail,
        v => v.Schedules.Select((p, i) => p switch
        {
          CronScheduleAttribute cron => ConfigureCronTrigger(v.Detail, cron.Value, i),
          TimeSpanScheduleAttribute ts => ConfigureTimespanTrigger(v.Detail, ts.Value, i),
          _ => throw new ArgumentException("Invalid schedule type")
        }).ToList())
      .ToDictionary<KeyValuePair<IJobDetail, List<ITrigger>>, IJobDetail, IReadOnlyCollection<ITrigger>>(
        jobConfig => jobConfig.Key, jobConfig => jobConfig.Value);
    
    logger.LogInformation("Scheduling {JobCount} jobs with {TriggerCount} triggers", jobConfigs.Count, jobConfigs.Values.Select(p => p.Count).Sum());
    await scheduler!.ScheduleJobs(jobConfigs, true);
    logger.LogInformation("Job scheduled");

    if (config != null)
    {
      logger.LogInformation("Additional configuration found");
      config.Invoke(scheduler);
    }
    else
    {
      logger.LogInformation("No additional configuration found");
    }
    
    logger.LogInformation("Scheduling health check job");
    await QuartzHealthCheckExtensions.ScheduleQuartzCheck(scheduler,
      host.Services.GetRequiredService<IOptions<QuartzHealthCheckOptions>>());
    logger.LogInformation("Scheduled health check job");

    return host;
  }

  private static IEnumerable<Type> FindConfiguredJobTypes(Assembly assembly)
  {
    return assembly.GetTypes()
        .Where(p => p.IsAssignableTo(typeof(IJob)) && 
                                          (p.GetCustomAttribute<CronScheduleAttribute>() != null) ||
                                          p.GetCustomAttribute<TimeSpanScheduleAttribute>() != null);
  }
  
  private static ITrigger ConfigureCronTrigger(IJobDetail job, string cronExpression, int index)
  {
    return TriggerBuilder.Create()
      .WithIdentity($"{job.JobType.Name}_{index}_trigger")
      .WithSchedule(
        CronScheduleBuilder.CronSchedule(cronExpression))
      .StartNow()
      .ForJob(job)
      .Build();
  }
  
  private static ITrigger ConfigureTimespanTrigger(IJobDetail job, TimeSpan time, int index)
  {
    return TriggerBuilder.Create()
      .WithIdentity($"{job.JobType.Name}_{index}_trigger")
      .WithSchedule(
        CronScheduleBuilder.DailyAtHourAndMinute(time.Hours, time.Minutes))
      .StartNow()
      .ForJob(job)
      .Build();
  }

  public static async Task<IEnumerable<object>> GetTriggers(this IScheduler scheduler)
  {
    var keys = await scheduler.GetTriggerKeys(global::Quartz.Impl.Matchers.GroupMatcher<TriggerKey>.AnyGroup());
    var list = new List<ITrigger>();
    foreach (var key in keys)
      list.Add((await scheduler.GetTrigger(key))!);

    return list;
  }
}