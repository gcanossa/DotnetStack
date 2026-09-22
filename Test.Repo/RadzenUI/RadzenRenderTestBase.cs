using Bunit;
using GKit.BlazorExt;
using GKit.UI.Localization;
using GKit.UI.RadzenExt;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using Test.Repo.UI.Shared;

namespace Test.Repo.RadzenUI;

/// <summary>
/// bUnit host for the Radzen adapter's components. The mirror of
/// <c>Test.Repo.UI.MudRenderTestBase</c>.
/// </summary>
public abstract class RadzenRenderTestBase : BunitContext, IAsyncLifetime
{
  protected RadzenRenderTestBase()
  {
    JSInterop.Mode = JSRuntimeMode.Loose;

    Services.AddLogging();
    Services.AddRadzenComponents();
    Services.AddGKitBlazorServices();
    Services.AddGKitRadzenUi();

    Services.AddGKitValidator<Widget, WidgetValidator>();
    Services.AddGKitValidator<Category, CategoryValidator>();
  }

  /// <summary>Swaps the neutral English strings for the culture-aware resources.</summary>
  protected void UseLocalizedStrings() => Services.AddGKitUiLocalization();

  Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

  /// <summary>
  /// Disposes asynchronously; Radzen, like MudBlazor, registers services that implement only
  /// <see cref="IAsyncDisposable"/>.
  /// </summary>
  async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
}
