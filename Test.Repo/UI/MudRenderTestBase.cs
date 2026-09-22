using Bunit;
using GKit.BlazorExt;
using GKit.UI;
using GKit.UI.Localization;
using GKit.UI.MudBlazorExt;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Test.Repo.UI.Shared;

namespace Test.Repo.UI;

/// <summary>
/// bUnit host for the MudBlazor adapter's components.
/// </summary>
/// <remarks>
/// Phase 0 left the Razor markup checked only by the compiler; these tests actually render it.
/// JS interop runs in loose mode because MudDataGrid's virtualisation, popovers and resize
/// observers all call into JS that has no meaning outside a browser.
/// </remarks>
public abstract class MudRenderTestBase : BunitContext, IAsyncLifetime
{
  protected MudRenderTestBase()
  {
    JSInterop.Mode = JSRuntimeMode.Loose;

    Services.AddLogging();
    Services.AddMudServices();
    Services.AddGKitBlazorServices();
    Services.AddGKitMudBlazorUi();
  }

  /// <summary>Swaps the neutral English strings for the culture-aware resources.</summary>
  protected void UseLocalizedStrings() => Services.AddGKitUiLocalization();

  protected static GridPage<Widget> Page(IEnumerable<Widget> items, int total) =>
    new() { Items = [.. items], TotalItems = total };

  Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

  /// <summary>
  /// Disposes asynchronously. MudBlazor registers services that implement only
  /// <see cref="IAsyncDisposable"/>, which the synchronous container teardown refuses to handle.
  /// </summary>
  async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
}
