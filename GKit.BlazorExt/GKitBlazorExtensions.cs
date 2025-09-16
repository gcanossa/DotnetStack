using System.ComponentModel;
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
  
  public static IServiceCollection AddNotifyingCascadingValue<T>(
    this IServiceCollection ext, T state, bool isFixed = false)
    where T : INotifyPropertyChanged
  {
    return ext.AddCascadingValue<T>(sp => new CascadingStateValueSource<T>(state, isFixed));
  }
}