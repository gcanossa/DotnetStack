using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GKit.UI.Localization;

public static class GKitUiLocalizationExtensions
{
  /// <summary>
  /// Replaces the neutral English strings with culture-aware resources.
  /// </summary>
  /// <remarks>
  /// Order-independent with respect to <c>AddGKitUiCore()</c>: this replaces any existing
  /// registration, and the core registration is conditional.
  /// </remarks>
  public static IServiceCollection AddGKitUiLocalization(this IServiceCollection services)
  {
    services.Replace(ServiceDescriptor.Scoped<IGKitUiStrings, ResourceUiStrings>());

    return services;
  }
}
