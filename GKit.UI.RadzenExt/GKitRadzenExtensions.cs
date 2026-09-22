using GKit.UI.Data;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace GKit.UI.RadzenExt;

public static class GKitRadzenExtensions
{
  /// <summary>
  /// Registers the Radzen implementations of the GKit UI shell abstractions.
  /// </summary>
  /// <remarks>
  /// Also calls <see cref="GKitUiDataExtensions.AddGKitUiCore"/>, so the neutral services are
  /// always present. Call Radzen's own <c>AddRadzenComponents()</c> separately and render
  /// <c>&lt;RadzenComponents /&gt;</c> in your layout.
  /// </remarks>
  public static IServiceCollection AddGKitRadzenUi(this IServiceCollection services)
  {
    services.AddGKitUiCore();

    services.AddScoped<IUiNotifier, RadzenUiNotifier>();
    services.AddScoped<IUiDialogs, RadzenUiDialogs>();
    services.AddScoped<IUiDialogHost, RadzenUiDialogHost>();
    services.AddScoped<IUiIconSet, RadzenUiIconSet>();

    return services;
  }

  /// <summary>
  /// Registers a validator so <see cref="GKitFluentValidator{T}"/> can resolve it.
  /// </summary>
  /// <remarks>
  /// MudBlazor's dialog injects the validator by its concrete type; the EditContext bridge
  /// resolves it as <c>IValidator&lt;T&gt;</c> instead, so it needs both registrations.
  /// </remarks>
  public static IServiceCollection AddGKitValidator<T, TValidator>(this IServiceCollection services)
    where T : class
    where TValidator : AbstractValidatorBase<T>
  {
    services.AddScoped<TValidator>();
    services.AddScoped<FluentValidation.IValidator<T>>(sp => sp.GetRequiredService<TValidator>());

    return services;
  }
}
