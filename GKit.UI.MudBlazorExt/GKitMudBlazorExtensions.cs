using GKit.UI.Data;
using Microsoft.Extensions.DependencyInjection;

namespace GKit.UI.MudBlazorExt;

public static class GKitMudBlazorExtensions
{
  /// <summary>
  /// Registers the MudBlazor implementations of the GKit UI shell abstractions.
  /// </summary>
  /// <remarks>
  /// Also calls <see cref="GKitUiDataExtensions.AddGKitUiCore"/>, so the neutral services are
  /// always present. Call MudBlazor's own <c>AddMudServices()</c> separately - this does not wrap
  /// it, because applications configure snackbar and dialog defaults there.
  /// </remarks>
  public static IServiceCollection AddGKitMudBlazorUi(this IServiceCollection services)
  {
    services.AddGKitUiCore();

    services.AddScoped<IUiNotifier, MudUiNotifier>();
    services.AddScoped<IUiDialogs, MudUiDialogs>();
    services.AddScoped<IUiIconSet, MudUiIconSet>();

    return services;
  }
}
