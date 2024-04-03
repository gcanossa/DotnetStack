using Microsoft.Extensions.DependencyInjection;

namespace GC.Blazor;

public static class GCBlazorExtensions
{
  public static IServiceCollection AddGCBlazorServices(this IServiceCollection ext)
  {
    ext.AddScoped<FullscreenService>();
    ext.AddScoped<DocumentEventService>();
    ext.AddScoped<TimerService>();
    ext.AddScoped<DownloadFileService>();

    return ext;
  }
}