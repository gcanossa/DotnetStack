using System.Reflection;
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
        services.AddOptions<GKitQuartzOptions>()
            .BindConfiguration("GKit:Quartz")
            // IsNullOrWhiteSpace, not IsNullOrEmpty(Trim()): configuration binding can leave a
            // `required` property null, and the old form threw NullReferenceException.
            .Validate(options => options.Schedules.TrueForAll(p => !string.IsNullOrWhiteSpace(p.JobTypeName)),
                "Schedule must have a non empty JobTypeName")
            .Validate(options => options.Schedules.TrueForAll(p => p.SpecifiedCount == 1),
                "Schedule must specify exactly one of Interval, DailyAt or CronExpression")
            .ValidateOnStart();

        services.AddOptions<QuartzHealthCheckOptions>()
            .BindConfiguration("GKit:Quartz:HealthCheck");

        // The probe job is always scheduled by UseGKitQuartz, so its dependency must always be
        // resolvable. Registering it only inside AddQuartzCheck meant a host that skipped the
        // health check hit an unresolvable-dependency error on every heartbeat, forever.
        services.AddSingleton<QuartzProbe>();

        services.AddQuartz(q => { q.SchedulerName = schedulerName ?? Assembly.GetEntryAssembly()!.FullName!; });
        services.AddQuartzHostedService(options =>
        {
            options.AwaitApplicationStarted = true;
            options.WaitForJobsToComplete = true;
        });

        return services;
    }

    public static IHost UseGKitQuartz(this IHost host, string? schedulerName = null, Action<IScheduler>? config = null,
        params Assembly[] otherAssemblies)
    {
        var appLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

        appLifetime.ApplicationStarted.Register(() =>
        {
            using var scope = host.Services.CreateScope();
            var schedulerFactory = scope.ServiceProvider.GetRequiredService<ISchedulerFactory>();
            var scheduler = schedulerFactory.GetScheduler(schedulerName ?? Assembly.GetEntryAssembly()!.FullName!)
                .ConfigureAwait(false).GetAwaiter().GetResult();

            var options = scope.ServiceProvider.GetRequiredService<IOptions<GKitQuartzOptions>>();

            if (scheduler is null)
                throw new Exception("Could not find a valid scheduler");

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<IScheduler>>();

            var entryAssembly = Assembly.GetEntryAssembly();
            var assemblies = new List<Assembly>();
            if (entryAssembly is not null) assemblies.Add(entryAssembly);
            assemblies.AddRange(otherAssemblies);

            var jobTypes = new List<Type>();
            foreach (var assembly in assemblies.Distinct())
            {
                logger.LogInformation("Scanning {Assembly} for jobs", assembly.FullName);
                var found = JobScheduleResolver.FindJobTypes(assembly).ToList();
                jobTypes.AddRange(found);
                logger.LogInformation("Found {Count}", found.Count);
            }

            var resolution = JobScheduleResolver.Resolve(jobTypes, options.Value);

            foreach (var (jobType, reason) in resolution.Skipped)
                logger.LogInformation("Skipping Job {JobType} {Reason}", jobType, reason);

            var jobConfigs = resolution.Scheduled.ToDictionary(
                job => job.Detail,
                job => (IReadOnlyCollection<ITrigger>)job.Triggers);

            logger.LogInformation("Scheduling {JobCount} jobs with {TriggerCount} triggers", jobConfigs.Count,
                jobConfigs.Values.Select(p => p.Count).Sum());
            scheduler.ScheduleJobs(jobConfigs, true).ConfigureAwait(false).GetAwaiter().GetResult();
            logger.LogInformation("Jobs scheduled");

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
            QuartzHealthCheckExtensions.ScheduleQuartzCheck(scheduler,
                    host.Services.GetRequiredService<IOptions<QuartzHealthCheckOptions>>())
                .ConfigureAwait(false).GetAwaiter().GetResult();
            logger.LogInformation("Scheduled health check job");
        });

        return host;
    }

    public static async Task<IEnumerable<ITrigger>> GetTriggers(this IScheduler scheduler)
    {
        var keys = await scheduler.GetTriggerKeys(global::Quartz.Impl.Matchers.GroupMatcher<TriggerKey>.AnyGroup());
        var list = new List<ITrigger>();
        foreach (var key in keys)
            list.Add((await scheduler.GetTrigger(key))!);

        return list;
    }
}