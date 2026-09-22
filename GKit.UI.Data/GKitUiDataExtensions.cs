using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GKit.UI.Data;

public static class GKitUiDataExtensions
{
  /// <summary>
  /// Registers the UI-neutral GKit services. Call this alongside an adapter's own registration
  /// (<c>AddGKitMudBlazorUi</c> or <c>AddGKitRadzenUi</c>).
  /// </summary>
  /// <remarks>
  /// <see cref="IGKitUiStrings"/> is registered with TryAdd so that referencing
  /// <c>GKit.UI.Localization</c> and calling <c>AddGKitUiLocalization()</c> first wins; otherwise
  /// the neutral English defaults apply.
  /// </remarks>
  public static IServiceCollection AddGKitUiCore(this IServiceCollection services)
  {
    services.TryAddScoped<IGKitUiStrings, DefaultUiStrings>();
    services.TryAddScoped(typeof(EditEntityDialogEngine<>));

    return services;
  }
}
