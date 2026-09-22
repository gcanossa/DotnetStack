using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Test.Repo.UI.MudBlazorExt.Components.Grids;
using Test.Repo.UI.MudBlazorExt.Components.Pages;
using Test.Repo.UI.Shared;

namespace Test.Repo.UI;

/// <summary>
/// Renders the MudBlazor adapter's grids through the demo host's own components.
/// </summary>
/// <remarks>
/// Phase 0 extracted the engine and unit tested it, but nothing rendered the Razor markup. These
/// tests close that gap by driving the real <c>WidgetGrid</c> and <c>WidgetReport</c> components,
/// which in turn use the real query factories, validators and dialog types from
/// <c>Test.Repo.UI.Shared</c>.
/// </remarks>
public class MudGridRenderTest : MudRenderTestBase
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

  private static List<string> CellTexts(IRenderedComponent<WidgetGrid> cut, string label) =>
    [.. cut.FindAll($"td[data-label='{label}']").Select(e => e.TextContent.Trim())];

  [Fact]
  public void EntityGrid_RendersToolbarTitle()
  {
    using var ctx = Seed(nameof(EntityGrid_RendersToolbarTitle));

    var cut = Render<WidgetGrid>(p => p.Add(x => x.SharedContext, ctx));

    Assert.Contains("Widgets", cut.Find(".mud-table-toolbar").TextContent);
  }

  [Fact]
  public void EntityGrid_RendersDeclaredColumnHeaders()
  {
    using var ctx = Seed(nameof(EntityGrid_RendersDeclaredColumnHeaders));

    var cut = Render<WidgetGrid>(p => p.Add(x => x.SharedContext, ctx));
    var markup = cut.Markup;

    foreach (var header in new[] { "Id", "Name", "Quantity", "Category", "Created", "Has notes" })
      Assert.Contains($">{header}<", markup);
  }

  [Fact]
  public void EntityGrid_LoadsSeededRowsThroughTheEngine()
  {
    using var ctx = Seed(nameof(EntityGrid_LoadsSeededRowsThroughTheEngine));

    var cut = Render<WidgetGrid>(p => p.Add(x => x.SharedContext, ctx));

    var names = CellTexts(cut, "Name");

    Assert.NotEmpty(names);
    Assert.Equal("Widget 001", names[0]);
    Assert.All(names, n => Assert.StartsWith("Widget ", n));
  }

  [Fact]
  public void EntityGrid_RendersIncludedNavigationProperty()
  {
    using var ctx = Seed(nameof(EntityGrid_RendersIncludedNavigationProperty));

    var cut = Render<WidgetGrid>(p => p.Add(x => x.SharedContext, ctx));

    var categories = CellTexts(cut, "Category");

    // Proves the shared QueryFactory's Include survived the neutral GridQuery round trip.
    Assert.Contains(categories, c => c is "Gaskets" or "Fasteners");
  }

  [Fact]
  public void EntityGrid_NullNavigation_RendersEmptyCellRatherThanThrowing()
  {
    using var ctx = Seed(nameof(EntityGrid_NullNavigation_RendersEmptyCellRatherThanThrowing));

    var cut = Render<WidgetGrid>(p => p.Add(x => x.SharedContext, ctx));

    var names = CellTexts(cut, "Name");
    var categories = CellTexts(cut, "Category");

    // Every third seeded widget has no category.
    var thirdRow = names.IndexOf("Widget 003");
    Assert.True(thirdRow >= 0, "Widget 003 should be on the first rendered page");
    Assert.Equal(string.Empty, categories[thirdRow]);
  }

  [Fact]
  public void EntityGrid_RendersDefaultAndCallerSuppliedRowControls()
  {
    using var ctx = Seed(nameof(EntityGrid_RendersDefaultAndCallerSuppliedRowControls));

    var cut = Render<WidgetGrid>(p => p.Add(x => x.SharedContext, ctx));

    var firstRowButtons = cut.FindAll("tbody tr")
      .First(r => r.QuerySelectorAll("td[data-label='Name']").Length > 0)
      .QuerySelectorAll("button");

    // Edit and Delete come from EntityGrid; Duplicate is passed in as a neutral GridRowControl.
    Assert.Equal(3, firstRowButtons.Length);
  }

  [Fact]
  public void EntityGrid_RowControlClick_InvokesCallbackWithThatRowsItem()
  {
    using var ctx = Seed(nameof(EntityGrid_RowControlClick_InvokesCallbackWithThatRowsItem));

    Widget? duplicated = null;

    var cut = Render<WidgetGrid>(p => p
      .Add(x => x.SharedContext, ctx)
      .Add(x => x.OnDuplicate, w => duplicated = w));

    var firstRow = cut.FindAll("tbody tr").First(r => r.QuerySelectorAll("td[data-label='Name']").Length > 0);
    firstRow.QuerySelectorAll("button")[2].Click();

    Assert.NotNull(duplicated);
    Assert.Equal("Widget 001", duplicated!.Name);
  }

  [Fact]
  public void EntityGrid_DisabledPredicate_DisablesThatRowsControl()
  {
    var db = nameof(EntityGrid_DisabledPredicate_DisablesThatRowsControl);
    using var ctx = Seed(db, widgets: 3);

    // The Duplicate control is disabled when Quantity is zero.
    ctx.Widgets.Add(new Widget { Id = 999, Name = "Widget 000", Quantity = 0, CreatedAt = DateTime.UtcNow });
    ctx.SaveChanges();
    ctx.ChangeTracker.Clear();

    var cut = Render<WidgetGrid>(p => p.Add(x => x.SharedContext, ctx));

    var rows = cut.FindAll("tbody tr").Where(r => r.QuerySelectorAll("td[data-label='Name']").Length > 0).ToList();

    var zeroRow = rows.Single(r => r.QuerySelector("td[data-label='Name']")!.TextContent.Trim() == "Widget 000");
    var normalRow = rows.First(r => r.QuerySelector("td[data-label='Name']")!.TextContent.Trim() == "Widget 001");

    Assert.True(zeroRow.QuerySelectorAll("button")[2].HasAttribute("disabled"));
    Assert.False(normalRow.QuerySelectorAll("button")[2].HasAttribute("disabled"));
  }

  [Fact]
  public void EntityGrid_NoData_ShowsNeutralEnglishEmptyState()
  {
    using var ctx = Empty(nameof(EntityGrid_NoData_ShowsNeutralEnglishEmptyState));

    var cut = Render<WidgetGrid>(p => p.Add(x => x.SharedContext, ctx));

    Assert.Contains("No records", cut.Markup);
  }

  [Fact]
  public void EntityGrid_NoData_ShowsItalianEmptyStateUnderLocalization()
  {
    using var ctx = Empty(nameof(EntityGrid_NoData_ShowsItalianEmptyStateUnderLocalization));
    UseLocalizedStrings();

    using var _ = new CultureScope("it-IT");

    var cut = Render<WidgetGrid>(p => p.Add(x => x.SharedContext, ctx));

    Assert.Contains("Nessun elemento presente", cut.Markup);
    Assert.DoesNotContain("No records", cut.Markup);
  }

  [Fact]
  public void ManagedGrid_HandWrittenNeutralLoader_RendersRows()
  {
    var db = nameof(ManagedGrid_HandWrittenNeutralLoader_RendersRows);
    Services.AddDbContextFactory<DemoDbContext>(o => o.UseInMemoryDatabase(db));
    using var seed = DemoData.Seed(db);

    // WidgetReport's loader is written against GridQuery/GridPage only - no MudBlazor type in its
    // signature - which is what makes that page portable to the Radzen adapter.
    var cut = Render<WidgetReport>();

    var names = cut.FindAll("td[data-label='Name']").Select(e => e.TextContent.Trim()).ToList();

    Assert.NotEmpty(names);
    Assert.Equal("Widget 001", names[0]);
  }
}
