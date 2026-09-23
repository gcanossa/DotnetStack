using GKit.Quartz;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace GKit.Tests.Quartz;

/// <summary>
/// Guards the registration shape of <see cref="GKitQuartzExtensions.AddGKitQuartz"/>: the scheduler
/// it adds is the container's default one, named through its <c>InstanceName</c>.
/// </summary>
/// <remarks>
/// Registering it with <c>AddQuartz(name, …)</c> instead puts every one of its parts behind the name
/// as a service key, leaving the unkeyed <see cref="ISchedulerFactory"/> unregistered — which is what
/// <c>UseGKitQuartz</c> resolves, and what a consumer pausing jobs around a command line run resolves
/// too. Both failed at startup with "No service for type 'Quartz.ISchedulerFactory'".
/// </remarks>
public class GKitQuartzRegistrationTests
{
  private static ServiceProvider Build(string schedulerName)
  {
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddGKitQuartz(schedulerName);
    return services.BuildServiceProvider();
  }

  [Fact]
  public async Task AddGKitQuartz_registers_an_unkeyed_scheduler_factory()
  {
    await using var provider = Build("registration-tests");

    var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler();

    Assert.Equal("registration-tests", scheduler.SchedulerName);
  }

  [Fact]
  // async for the disposal alone: a resolved scheduler is IAsyncDisposable, so the container it came
  // from can only be disposed asynchronously.
  public async Task AddGKitQuartz_registers_an_unkeyed_scheduler()
  {
    await using var provider = Build("registration-tests-scheduler");

    var scheduler = provider.GetService<IScheduler>();

    Assert.NotNull(scheduler);
    Assert.Equal("registration-tests-scheduler", scheduler.SchedulerName);
  }

  [Fact]
  public async Task The_named_scheduler_is_found_by_name()
  {
    await using var provider = Build("registration-tests-lookup");
    var factory = provider.GetRequiredService<ISchedulerFactory>();

    // What UseGKitQuartz does when it is handed a name: GetRequiredScheduler, not a keyed resolve.
    var scheduler = await factory.GetRequiredScheduler("registration-tests-lookup");

    Assert.Equal("registration-tests-lookup", scheduler.SchedulerName);
  }
}
