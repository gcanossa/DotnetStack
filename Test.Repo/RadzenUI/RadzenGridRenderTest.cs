using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Test.Repo.UI.Shared;
using RadzenWidgetGrid = Test.Repo.UI.Radzen.Components.Grids.WidgetGrid;

namespace Test.Repo.RadzenUI;

/// <summary>
/// Renders the Radzen adapter's grids through the Radzen host's own components, against the same
/// <c>Test.Repo.UI.Shared</c> domain the MudBlazor host uses.
/// </summary>
public class RadzenGridRenderTest : RadzenRenderTestBase
{
  private DemoDbContext Seed(string db, int widgets = 30)
  {
    Services.AddDbContextFactory<DemoDbContext>(o => o.UseInMemoryDatabase(db));
    return DemoData.Seed(db, widgets);
  }

  private DemoDbContext Empty(string db)
  {
    Services.AddDbContextFactory<DemoDbContext>(o => o.UseInMemoryDatabase(db));
    return DemoData.Create(db);
  }

  [Fact]
  public void EntityGrid_RendersToolbarTitle()
  {
    using var ctx = Seed(nameof(EntityGrid_RendersToolbarTitle));

    var cut = Render<RadzenWidgetGrid>(p => p.Add(x => x.SharedContext, ctx));

    Assert.Contains("Widgets", cut.Markup);
  }

  [Fact]
  public void EntityGrid_RendersDeclaredColumnHeaders()
  {
    using var ctx = Seed(nameof(EntityGrid_RendersDeclaredColumnHeaders));

    var cut = Render<RadzenWidgetGrid>(p => p.Add(x => x.SharedContext, ctx));
    var markup = cut.Markup;

    foreach (var header in new[] { "Id", "Name", "Quantity", "Category", "Created", "Has notes" })
      Assert.Contains(header, markup);
  }

  [Fact]
  public void EntityGrid_LoadsSeededRowsThroughTheSharedEngine()
  {
    using var ctx = Seed(nameof(EntityGrid_LoadsSeededRowsThroughTheSharedEngine));

    var cut = Render<RadzenWidgetGrid>(p => p.Add(x => x.SharedContext, ctx));

    // Same engine, same query factory, same seed as the MudBlazor host.
    Assert.Contains("Widget 001", cut.Markup);
  }

  [Fact]
  public void EntityGrid_RendersIncludedNavigationProperty()
  {
    using var ctx = Seed(nameof(EntityGrid_RendersIncludedNavigationProperty));

    var cut = Render<RadzenWidgetGrid>(p => p.Add(x => x.SharedContext, ctx));

    Assert.True(
      cut.Markup.Contains("Gaskets") || cut.Markup.Contains("Fasteners"),
      "the shared QueryFactory's Include should survive the neutral GridQuery round trip");
  }

  [Fact]
  public void EntityGrid_NoData_ShowsNeutralEnglishEmptyState()
  {
    using var ctx = Empty(nameof(EntityGrid_NoData_ShowsNeutralEnglishEmptyState));

    var cut = Render<RadzenWidgetGrid>(p => p.Add(x => x.SharedContext, ctx));

    Assert.Contains("No records", cut.Markup);
  }

  [Fact]
  public void EntityGrid_NoData_ShowsItalianEmptyStateUnderLocalization()
  {
    using var ctx = Empty(nameof(EntityGrid_NoData_ShowsItalianEmptyStateUnderLocalization));
    UseLocalizedStrings();

    using var _ = new Test.Repo.UI.CultureScope("it-IT");

    var cut = Render<RadzenWidgetGrid>(p => p.Add(x => x.SharedContext, ctx));

    Assert.Contains("Nessun elemento presente", cut.Markup);
  }
}
