using System.Text.Json;
using System.Text.Json.Serialization;
using GKit.Application;
#if (quartz)
using Quartz;
#endif

namespace GKit.App1.CliRunners;

/// <summary>
/// Shared base for the application's command line commands. Application.Wrap resolves every
/// registered ICommandLineRunner, runs the ones matching argv, and skips host.Run() when one of
/// them asks for it.
/// </summary>
public abstract class CommandLineRunnerBase : ICommandLineRunner
{
    public abstract bool ShouldDisableHostRun(IHost host, string[] args);

    public abstract bool Matches(IHost host, string[] args);

    public abstract string Help { get; }

    public virtual async Task Execute(IHost host, string[] args)
    {
#if (quartz)
        // A command runs inside the built host, so scheduled jobs would otherwise fire underneath it.
        var schedulers = await host.Services.GetRequiredService<ISchedulerFactory>().GetAllSchedulers();
        foreach (var scheduler in schedulers)
        {
            await scheduler.PauseAll();
        }
#else
        await Task.CompletedTask;
#endif
    }

    protected static string JsonSerialize<T>(T obj) =>
        JsonSerializer.Serialize(obj,
            new JsonSerializerOptions { WriteIndented = true, ReferenceHandler = ReferenceHandler.IgnoreCycles });
}
