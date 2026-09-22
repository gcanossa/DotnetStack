using System.Linq.Expressions;
using Bunit;
using GKit.Tests.Blazor.Infrastructure;
using GKit.UI;
using GKit.UI.Data;
using GKit.UI.MudBlazorExt;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace GKit.Tests.Blazor;

/// <summary>
/// The export reads <c>MudDataGrid.RenderedColumns</c>, so this needs a genuinely rendered grid -
/// the one place in this suite where bUnit's renderer is doing real work.
/// </summary>
/// <remarks>
/// The column-to-descriptor step now splits in two: the adapter projects rendered columns onto
/// neutral <see cref="ExportColumn"/> records, and <c>GKit.UI.Data</c> resolves and writes them.
/// This covers both halves end to end.
/// </remarks>
public class ExportColumnTests : TestContext, IDisposable
{
  private readonly GridFixture _fixture = new();
  private readonly IGKitUiStrings _strings = new DefaultUiStrings();

  public ExportColumnTests()
  {
    Services.AddMudServices();
    JSInterop.Mode = JSRuntimeMode.Loose;
  }

  public new void Dispose()
  {
    _fixture.Dispose();
    base.Dispose();
    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// MudDataGrid opens popovers for its column options, so the tree needs a MudPopoverProvider
  /// alongside it - exactly as a real layout would.
  /// </summary>
  private MudDataGrid<Product> RenderGrid(RenderFragment columns, IEnumerable<Product> items)
  {
    var host = Render(builder =>
    {
      builder.OpenComponent<MudPopoverProvider>(0);
      builder.CloseComponent();

      builder.OpenComponent<MudDataGrid<Product>>(1);
      builder.AddComponentParameter(2, nameof(MudDataGrid<Product>.Items), items);
      builder.AddComponentParameter(3, nameof(MudDataGrid<Product>.Columns), columns);
      builder.CloseComponent();
    });

    return host.FindComponent<MudDataGrid<Product>>().Instance;
  }

  private async Task<ISheet> ExportAsync(IQueryable<Product> query, MudDataGrid<Product> grid)
  {
    using var ms = new MemoryStream();
    await query.ToXlsAsync("Report", grid.ToExportColumns(_strings), ms);
    ms.Position = 0;
    return new XSSFWorkbook(ms).GetSheetAt(0);
  }

  private static void Column<TProp>(RenderTreeBuilder builder, int seq, Expression<Func<Product, TProp>> property)
  {
    builder.OpenComponent<PropertyColumn<Product, TProp>>(seq);
    builder.AddComponentParameter(seq + 1, nameof(PropertyColumn<Product, TProp>.Property), property);
    builder.CloseComponent();
  }

  [Fact]
  public async Task Plain_property_columns_are_exported()
  {
    List<Product> items = [new() { Name = "a", Price = 1.5m }];

    var grid = RenderGrid(builder => Column<string>(builder, 0, x => x.Name), items);

    var sheet = await ExportAsync(items.AsQueryable(), grid);

    Assert.Equal("Name", sheet.GetRow(0).GetCell(0).StringCellValue);
    Assert.Equal("a", sheet.GetRow(1).GetCell(0).StringCellValue);
  }

  [Fact]
  public async Task A_column_that_is_not_a_plain_property_chain_is_skipped_not_fatal()
  {
    // A computed expression yields a PropertyName that GetProperty cannot resolve; the old
    // `GetProperty(segment)!` then NRE'd inside Aggregate and took the whole export down.
    List<Product> items = [new() { Name = "a", Price = 1.5m }];

    var grid = RenderGrid(builder =>
    {
      Column<string>(builder, 0, x => x.Name);
      // Arithmetic, not a property chain: nothing for GetProperty to resolve.
      Column<decimal>(builder, 2, x => x.Price * 2);
    }, items);

    var sheet = await ExportAsync(items.AsQueryable(), grid);

    // The resolvable column still exported; the computed one was dropped rather than throwing.
    Assert.Equal("Name", sheet.GetRow(0).GetCell(0).StringCellValue);
    Assert.Equal("a", sheet.GetRow(1).GetCell(0).StringCellValue);
  }

  [Fact]
  public async Task Numeric_columns_reach_the_sheet_as_numbers()
  {
    List<Product> items = [new() { Name = "a", Price = 12.5m }];

    var grid = RenderGrid(builder => Column<decimal>(builder, 0, x => x.Price), items);

    var sheet = await ExportAsync(items.AsQueryable(), grid);
    var cell = sheet.GetRow(1).GetCell(0);

    Assert.Equal(CellType.Numeric, cell.CellType);
    Assert.Equal(12.5, cell.NumericCellValue, 5);
  }

  [Fact]
  public async Task Template_columns_are_not_exported()
  {
    // A template column has no underlying property to read.
    List<Product> items = [new() { Name = "a", Price = 1.5m }];

    var grid = RenderGrid(builder =>
    {
      Column<string>(builder, 0, x => x.Name);

      builder.OpenComponent<TemplateColumn<Product>>(2);
      builder.AddComponentParameter(3, nameof(TemplateColumn<Product>.Title), "Actions");
      builder.CloseComponent();
    }, items);

    var sheet = await ExportAsync(items.AsQueryable(), grid);

    Assert.Equal("Name", sheet.GetRow(0).GetCell(0).StringCellValue);
    Assert.Null(sheet.GetRow(0).GetCell(1));
  }
}
