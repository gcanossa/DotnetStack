using GKit.Reporting;
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
  /// <para>
  /// <see cref="IGKitUiStrings"/> is registered with TryAdd so that referencing
  /// <c>GKit.UI.Localization</c> and calling <c>AddGKitUiLocalization()</c> first wins; otherwise
  /// the neutral English defaults apply.
  /// </para>
  /// <para>
  /// <see cref="XlsTheme"/> is the house style every grid export falls back to, so registering one
  /// of your own before this call restyles every export in the application at once. It is a
  /// singleton because a theme holds no workbook-bound state — only the description of one.
  /// </para>
  /// </remarks>
  public static IServiceCollection AddGKitUiCore(this IServiceCollection services)
  {
    services.TryAddScoped<IGKitUiStrings, DefaultUiStrings>();
    services.TryAddScoped(typeof(EditEntityDialogEngine<>));
    services.TryAddSingleton(XlsTheme.Default);

    return services;
  }
}
