using Serilog;
using Serilog.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace GKit.Application;

public static class Application
{
  public static void Wrap(Action app)
  {
    Log.Logger = new LoggerConfiguration()
      .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
      .Enrich.FromLogContext()
      .WriteTo.Console()
      .CreateBootstrapLogger();

    try
    {
      Log.Information("Starting web application");

      app();
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
