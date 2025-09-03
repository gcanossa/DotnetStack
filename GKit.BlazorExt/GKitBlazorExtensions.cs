using Microsoft.Extensions.DependencyInjection;

namespace GKit.BlazorExt;

public static class GKitBlazorExtensions
{
  public static IServiceCollection AddGKitBlazorServices(this IServiceCollection ext)
  {
    ext.AddScoped<HtmlAttrSetterService>();
    ext.AddScoped<FullscreenService>();
    ext.AddScoped<DocumentKeyboardEventService>();
    ext.AddScoped<DocumentPasteEventService>();
    ext.AddScoped<DocumentDropEventService>();
    ext.AddScoped<TimerService>();
    ext.AddScoped<DownloadFileService>();
    ext.AddScoped<ClipboardService>();

    return ext;
  }
}