using System.Reflection;
using Quartz;

namespace GKit.Quartz;

/// <summary>
/// Turns discovered job types plus <see cref="GKitQuartzOptions"/> into the job/trigger map.
/// <para>
/// Lifted out of the <c>ApplicationStarted</c> callback in <c>UseGKitQuartz</c> so the
/// enable/disable precedence, the schedule semantics and the type filtering can be tested
/// without standing up a host and a scheduler.
/// </para>
/// </summary>
public static class JobScheduleResolver
{
  public sealed record JobSchedule(Type JobType, IJobDetail Detail, IReadOnlyList<ITrigger> Triggers);

  public sealed record SkippedJob(Type JobType, string Reason);

  public sealed record Resolution(
    IReadOnlyList<JobSchedule> Scheduled,
    IReadOnlyList<SkippedJob> Skipped);

  /// <summary>
  /// Finds concrete, instantiable <see cref="IJob"/> implementations.
  /// <para>
  /// Filtering on <c>IsAssignableTo(typeof(IJob))</c> alone also matched <c>IJob</c> itself,
  /// abstract bases and open generics, all of which make <c>JobBuilder.OfType</c> throw at
  /// startup. <c>GetTypes()</c> additionally throws on assemblies with unresolvable references.
  /// </para>
  /// </summary>
  public static IEnumerable<Type> FindJobTypes(Assembly assembly)
  {
    ArgumentNullException.ThrowIfNull(assembly);

    Type?[] types;
    try
    {
      types = assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException e)
    {
      types = e.Types;
    }

    return types
      .Where(t => t is not null)
      .Select(t => t!)
      .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                  && t.IsAssignableTo(typeof(IJob)));
  }

  /// <summary>
  /// A job runs unless an allow-list excludes it or the deny-list names it.
  /// <strong>Deny always wins</strong>: a deny-list entry is an explicit kill switch, so it
  /// stays effective even when an allow-list is also configured.
  /// <para>
  /// The original expression was
  /// <c>EnabledJobs?.Any(...) ?? true &amp;&amp; !(DisabledJobs?.Any(...) ?? false)</c>.
  /// <c>&amp;&amp;</c> binds tighter than <c>??</c>, so it parsed as
  /// <c>EnabledJobs?.Any(...) ?? (true &amp;&amp; !Disabled)</c> and the deny-list was dead code
  /// whenever an allow-list was configured.
  /// </para>
  /// </summary>
  public static bool IsEnabled(Type jobType, GKitQuartzOptions? options)
  {
    ArgumentNullException.ThrowIfNull(jobType);

    // FullName is null only for generic parameters and array/pointer constructs, none of
    // which reach here — but configuration matching is by name, so bail rather than guess.
    var name = jobType.FullName;
    if (name is null) return false;

    if (options?.DisabledJobs?.Contains(name) == true)
      return false;

    if (options?.EnabledJobs is { Count: > 0 } allowed)
      return allowed.Contains(name);

    return true;
  }

  public static IReadOnlyList<object> SchedulesFor(Type jobType, GKitQuartzOptions? options)
  {
    ArgumentNullException.ThrowIfNull(jobType);

    var fromAttributes = jobType.GetCustomAttributes<CronScheduleAttribute>().Cast<object>()
      .Concat(jobType.GetCustomAttributes<IntervalScheduleAttribute>())
      .Concat(jobType.GetCustomAttributes<DailyAtScheduleAttribute>());

    var fromOptions = (options?.Schedules ?? [])
      .Where(s => s.JobTypeName == jobType.FullName)
      .Select(ToScheduleDescriptor)
      .Where(s => s is not null)
      .Select(s => s!);

    return [.. fromAttributes, .. fromOptions];
  }

  private static object? ToScheduleDescriptor(GKitQuartzOptions.ScheduleOptions options) =>
    options.CronExpression is not null ? new CronScheduleAttribute(options.CronExpression)
    : options.Interval is not null ? new IntervalScheduleAttribute(options.Interval.Value)
    : options.DailyAt is not null ? new DailyAtScheduleAttribute(options.DailyAt.Value)
    : null;

  public static Resolution Resolve(IEnumerable<Type> jobTypes, GKitQuartzOptions? options)
  {
    ArgumentNullException.ThrowIfNull(jobTypes);

    var scheduled = new List<JobSchedule>();
    var skipped = new List<SkippedJob>();

    foreach (var jobType in jobTypes.Distinct())
    {
      if (!IsEnabled(jobType, options))
      {
        skipped.Add(new SkippedJob(jobType, "DISABLED"));
        continue;
      }

      var schedules = SchedulesFor(jobType, options);
      if (schedules.Count == 0)
      {
        skipped.Add(new SkippedJob(jobType, "NO SCHEDULES"));
        continue;
      }

      // A deterministic key: a fresh Guid per start orphans a row per job per restart in a
      // persistent job store, and makes ScheduleJobs(replace: true) unable to match the old one.
      var detail = JobBuilder.Create().OfType(jobType)
        .WithIdentity(jobType.FullName!, "gkit")
        .Build();

      var triggers = schedules
        .Select((schedule, index) => BuildTrigger(detail, jobType, schedule, index))
        .ToList();

      scheduled.Add(new JobSchedule(jobType, detail, triggers));
    }

    return new Resolution(scheduled, skipped);
  }

  private static ITrigger BuildTrigger(IJobDetail job, Type jobType, object schedule, int index)
  {
    // Scoped by the job's full name: `{TypeName}_{index}_trigger` collided whenever the same
    // type name appeared in two assemblies.
    var builder = TriggerBuilder.Create()
      .WithIdentity($"{jobType.FullName}_{index}", "gkit")
      .StartNow()
      .ForJob(job);

    return schedule switch
    {
      CronScheduleAttribute cron =>
        builder.WithSchedule(CronScheduleBuilder.Create(cron.Value)).Build(),

      IntervalScheduleAttribute interval =>
        builder.WithSchedule(SimpleScheduleBuilder.Create()
          .WithInterval(interval.Value).RepeatForever()).Build(),

      // CronScheduleBuilder.DailyAtHourAndMinute was removed in Quartz 4; this is the same
      // expression it used to build (seconds=0, day-of-month unspecified, every month/weekday).
      DailyAtScheduleAttribute dailyAt =>
        builder.WithSchedule(CronScheduleBuilder
          .Create($"0 {dailyAt.Value.Minutes} {dailyAt.Value.Hours} ? * *")).Build(),

      _ => throw new ArgumentException($"Invalid schedule type {schedule.GetType().Name}")
    };
  }
}
