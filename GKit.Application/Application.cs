using Serilog;
using Serilog.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GKit.Application;

public static class Application
{
  /// <summary>
  /// Runs the host, dispatching any matching <see cref="ICommandLineRunner"/> first.
  /// Returns the process exit code: 0 on success, 1 if the application terminated unexpectedly.
  /// </summary>
  public static int WrapAsync(string[] args, Func<IHost> app) => RunAsync(args, app)
    .ConfigureAwait(false).GetAwaiter().GetResult();

  public static void Wrap(string[] args, Func<IHost> app)
  {
    // A fatal error used to be logged and then reported as a clean exit. Docker
    // `restart: on-failure`, systemd `Restart=on-failure`, Kubernetes and CI all read the exit
    // code, so a container that could not reach its database sat "successfully exited" forever.
    Environment.ExitCode = WrapAsync(args, app);
  }

  public static async Task<int> RunAsync(string[] args, Func<IHost> app)
  {
    Log.Logger = new LoggerConfiguration()
      .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
      .Enrich.FromLogContext()
      .WriteTo.Console()
      .CreateBootstrapLogger();

    try
    {
      Log.Information("Starting web application");

      // IHost owns the root service provider: every singleton IDisposable (DbContext
      // factories, PLC/OPC UA sessions, SmtpServer) leaks without this.
      using var host = app();

      var runners = host.Services.GetRequiredService<IEnumerable<ICommandLineRunner>>().ToList();

      if (args.Contains("--help"))
      {
        Console.WriteLine("Available commands:");
        foreach (var runner in runners)
        {
          Console.WriteLine(runner.Help);
        }
        return 0;
      }

      var shouldRun = true;
      foreach (var runner in runners.Where(p => p.Matches(host, args)))
      {
        var runnerName = runner.GetType().FullName;
        if (shouldRun && runner.ShouldDisableHostRun(host, args))
        {
          shouldRun = false;
          Log.Information("Host Run Disabled by CommandLineRunner {Runner}", runnerName);
        }

        Log.Information("Running CommandLineRunner {Runner}", runnerName);
        await runner.Execute(host, args).ConfigureAwait(false);
        Log.Information("Executed CommandLineRunner {Runner}", runnerName);
      }

      if (shouldRun)
        await host.RunAsync().ConfigureAwait(false);

      return 0;
    }
    catch (Exception ex)
    {
      Log.Fatal(ex, "Application terminated unexpectedly");
      return 1;
    }
    finally
    {
      await Log.CloseAndFlushAsync().ConfigureAwait(false);
    }
  }
}
