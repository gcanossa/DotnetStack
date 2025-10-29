using Serilog;
using Serilog.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GKit.Application;

public static class Application
{
  public static void Wrap(string[] args, Func<IHost> app)
  {
    Log.Logger = new LoggerConfiguration()
      .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
      .Enrich.FromLogContext()
      .WriteTo.Console()
      .CreateBootstrapLogger();

    try
    {
      Log.Information("Starting web application");

      var host = app();
      var runners = host.Services.GetRequiredService<IEnumerable<ICommandLineRunner>>().ToList();

      if (args.Length == 1 && args[0] == "--help")
      {
        Console.WriteLine("Available commands:");
        foreach (var runner in runners)
        {
          Console.WriteLine(runner.Help);
        }
        return;
      }
      
      var shouldRun = true;
      foreach (var runner in runners.Where(p => p.Matches(host, args)))
      {
        var runnerName = runner.GetType().FullName;
        if (shouldRun && runner.ShouldDisableHostRun(host, args))
        {
          shouldRun = false;
          Log.Information("Host Run Disabled by CommandLinRunner {Runner}", runnerName);
        }
        
        Log.Information("Running CommandLineRunner {Runner}", runnerName);
        runner.Execute(host, args).ConfigureAwait(false).GetAwaiter().GetResult();
        Log.Information("Executed CommandLineRunner {Runner}", runnerName);
      }
      
      if (shouldRun)
        host.Run();
    }
    catch (Exception ex)
    {
      Log.Fatal(ex, "Application terminated unexpectedly");
    }
    finally
    {
      Log.CloseAndFlush();
    }
  }
}
