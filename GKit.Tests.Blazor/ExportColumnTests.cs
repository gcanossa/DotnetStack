using Bunit;
using Microsoft.AspNetCore.Components;
using GKit.MudBlazorExt;
using GKit.Tests.Blazor.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace GKit.Tests.Blazor;

/// <summary>
/// <c>ToXlsAsync</c> reads <c>MudDataGrid.RenderedColumns</c>, so this needs a genuinely
/// rendered grid — the one place in this suite where bUnit's renderer is doing real work.
/// </summary>
public class ExportColumnTests : TestContext, IDisposable
{
  private readonly GridFixture _fixture = new();

  public ExportColumnTests()
  {
    Services.AddMudServices();
    JSInterop.Mode = JSRuntimeMode.Loose;
  }

  public new void Dispose()
  {
    _fixture.Dispose();
    base.Dispose();
  }

  /// <summary>
  /// MudDataGrid opens popovers for its column options, so the tree needs a
  /// MudPopoverProvider alongside it — exactly as a real layout would.
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

  private static async Task<ISheet> ExportAsync(IQueryable<Product> query, MudDataGrid<Product> grid)
  {
    using var ms = new MemoryStream();
    await query.ToXlsAsync("Report", grid, ms);
    ms.Position = 0;
    return new XSSFWorkbook(ms).GetSheetAt(0);
  }

  [Fact]
  public async Task Plain_property_columns_are_exported()
  {
    List<Product> items = [new() { Name = "a", Price = 1.5m }];

    var grid = RenderGrid(builder =>
    {
      builder.OpenComponent<PropertyColumn<Product, string>>(0);
      builder.AddComponentParameter(1, nameof(PropertyColumn<Product, string>.Property),
        (System.Linq.Expressions.Expression<Func<Product, string>>)(x => x.Name));
      builder.CloseComponent();
    }, items);

    var sheet = await ExportAsync(items.AsQueryable(), grid);

    Assert.Equal("Name", sheet.GetRow(0).GetCell(0).StringCellValue);
    Assert.Equal("a", sheet.GetRow(1).GetCell(0).StringCellValue);
  }

  [Fact]
  public async Task A_column_that_is_not_a_plain_property_chain_is_skipped_not_fatal()
  {
    // PropertyName for a computed expression cannot be resolved with GetProperty; the old
    // `GetProperty(cur)!` then NRE'd inside Aggregate and took the whole export down.
    List<Product> items = [new() { Name = "a", Price = 1.5m }];

    var grid = RenderGrid(builder =>
    {
      builder.OpenComponent<PropertyColumn<Product, string>>(0);
      builder.AddComponentParameter(1, nameof(PropertyColumn<Product, string>.Property),
        (System.Linq.Expressions.Expression<Func<Product, string>>)(x => x.Name));
      builder.CloseComponent();

      builder.OpenComponent<PropertyColumn<Product, int>>(2);
      builder.AddComponentParameter(3, nameof(PropertyColumn<Product, int>.Property),
        (System.Linq.Expressions.Expression<Func<Product, int>>)(x => x.Name.Length));
      builder.CloseComponent();
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

    var grid = RenderGrid(builder =>
    {
      builder.OpenComponent<PropertyColumn<Product, decimal>>(0);
      builder.AddComponentParameter(1, nameof(PropertyColumn<Product, decimal>.Property),
        (System.Linq.Expressions.Expression<Func<Product, decimal>>)(x => x.Price));
      builder.CloseComponent();
    }, items);

    var sheet = await ExportAsync(items.AsQueryable(), grid);
    var cell = sheet.GetRow(1).GetCell(0);

    Assert.Equal(CellType.Numeric, cell.CellType);
    Assert.Equal(12.5, cell.NumericCellValue, 5);
  }
}
