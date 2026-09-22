using GKit.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
#if (quartz)
using GKit.Quartz;
#endif
using Serilog;

// Same entry point as the Blazor hosts: Application.Wrap gives a worker --help and one-off
// commands through ICommandLineRunner without a separate console project.
Application.Wrap(args, () =>
{
    var builder = Host.CreateApplicationBuilder(args);

    // Host.CreateApplicationBuilder(args) already loads appsettings.json and
    // appsettings.{Environment}.json, so there is nothing to add here.

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

#if (host_windows)
    builder.Services.AddWindowsService();
#endif
#if (host_systemd)
    builder.Services.AddSystemd();
#endif

#if (quartz)
    builder.Services.AddGKitQuartz();
#endif

    var host = builder.Build();

#if (quartz)
    host.UseGKitQuartz();
#endif

    return host;
});
