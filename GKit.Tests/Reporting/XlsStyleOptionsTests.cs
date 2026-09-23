using GKit.Reporting;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace GKit.Tests.Reporting;

/// <summary>
/// Styling without a subclass: <see cref="XlsStyleOptions{T}"/> is what the grid export and any
/// other caller reaches for, so these cover the theme, the per-cell resolver, the column format
/// and the caching that keeps a large export under Excel's cell-style ceiling.
/// </summary>
public class XlsStyleOptionsTests
{
  public class Movement
  {
    public string Code { get; set; } = "";
    public decimal Quantity { get; set; }
    public DateTime Date { get; set; }
  }

  private static IEnumerable<ColumnDescriptor<Movement>> Descriptors(string? quantityFormat = null) =>
    new DescriptorsBuilder<Movement>()
      .Column("Codice", p => p.Code)
      .Column("Quantita", p => p.Quantity, quantityFormat)
      .Column("Data", p => p.Date)
      .Build();

  private static async Task<ISheet> RenderAsync(XlsReporter<Movement> reporter, IEnumerable<Movement> data)
  {
    using var ms = new MemoryStream();
    await reporter.WriteReportAsync(data, ms);
    ms.Position = 0;
    return new XSSFWorkbook(ms).GetSheetAt(0);
  }

  private static XSSFFont FontOf(ICell cell) => (XSSFFont)((XSSFCellStyle)cell.CellStyle).GetFont();

  [Fact]
  public async Task Default_options_keep_the_look_reports_already_had()
  {
    IEnumerable<Movement> data = [new() { Code = "A" }];

    var sheet = await RenderAsync(new XlsReporter<Movement>("R", Descriptors()), data);

    var header = FontOf(sheet.GetRow(0).GetCell(0));
    Assert.Equal("Tahoma", header.FontName);
    Assert.Equal(8, header.FontHeightInPoints);
    Assert.True(header.IsBold);

    var cell = sheet.GetRow(1).GetCell(0);
    Assert.False(FontOf(cell).IsBold);
    Assert.Equal(BorderStyle.Thin, cell.CellStyle.BorderBottom);
    Assert.Equal(VerticalAlignment.Center, cell.CellStyle.VerticalAlignment);
  }

  [Fact]
  public async Task A_theme_restyles_the_whole_sheet()
  {
    var styles = new XlsStyleOptions<Movement>
    {
      Theme = XlsTheme.Default with
      {
        FontFamily = "Calibri",
        FontSize = 11,
        HeaderBackground = IndexedColors.Grey25Percent,
        Border = BorderStyle.Medium
      }
    };
    IEnumerable<Movement> data = [new() { Code = "A" }];

    var sheet = await RenderAsync(new XlsReporter<Movement>("R", Descriptors(), styles), data);

    var header = sheet.GetRow(0).GetCell(0);
    Assert.Equal("Calibri", FontOf(header).FontName);
    Assert.Equal(11, FontOf(header).FontHeightInPoints);
    Assert.Equal(FillPattern.SolidForeground, header.CellStyle.FillPattern);
    Assert.Equal(IndexedColors.Grey25Percent.Index, header.CellStyle.FillForegroundColor);

    var cell = sheet.GetRow(1).GetCell(0);
    Assert.Equal("Calibri", FontOf(cell).FontName);
    Assert.Equal(BorderStyle.Medium, cell.CellStyle.BorderBottom);
    // Data cells are not filled: the background belongs to the header role alone.
    Assert.NotEqual(FillPattern.SolidForeground, cell.CellStyle.FillPattern);
  }

  [Fact]
  public async Task A_resolver_claims_the_cells_it_wants_and_leaves_the_rest_to_the_theme()
  {
    var styles = new XlsStyleOptions<Movement>
    {
      Resolve = ctx => ctx is { Role: XlsCellRole.Data, Value: decimal and < 0 }
        ? new XlsCellStyle("negative", wb => wb.CreateCellStyle()
          .WithFont(wb.CreateFont().FontStyle("Tahoma", 8).Italic())
          .BorderStyle(BorderStyle.Thin))
        : null
    };
    IEnumerable<Movement> data = [new() { Quantity = -1m }, new() { Quantity = 1m }];

    var sheet = await RenderAsync(new XlsReporter<Movement>("R", Descriptors(), styles), data);

    Assert.True(FontOf(sheet.GetRow(1).GetCell(1)).IsItalic);
    Assert.False(FontOf(sheet.GetRow(2).GetCell(1)).IsItalic);
    // Cells the resolver declined keep the theme's style, untouched.
    Assert.False(FontOf(sheet.GetRow(1).GetCell(0)).IsItalic);
  }

  [Fact]
  public async Task A_resolver_reusing_one_key_creates_one_style()
  {
    // xlsx caps a workbook at roughly 64k cell styles: a resolver that built a style per cell
    // would take a 200-row export to 200 styles, and a real one past the ceiling.
    var styles = new XlsStyleOptions<Movement>
    {
      Resolve = ctx => ctx.Role == XlsCellRole.Data
        ? new XlsCellStyle("data-override", wb => wb.CreateCellStyle()
          .WithFont(wb.CreateFont().FontStyle("Tahoma", 8)))
        : null
    };
    var data = Enumerable.Range(0, 200).Select(i => new Movement { Code = $"C{i}", Quantity = i }).ToList();

    var sheet = await RenderAsync(new XlsReporter<Movement>("R", Descriptors(), styles), data);

    Assert.Equal(200, sheet.LastRowNum);
    // The built-ins NPOI seeds a workbook with, plus the handful this render added.
    Assert.InRange(sheet.Workbook.NumCellStyles, 1, 10);
  }

  [Fact]
  public async Task A_column_format_is_applied_over_the_style_the_cell_would_have_had()
  {
    var styles = new XlsStyleOptions<Movement>
    {
      Theme = XlsTheme.Default with { FontFamily = "Calibri" }
    };
    IEnumerable<Movement> data = [new() { Quantity = 1234.5m }];

    var sheet = await RenderAsync(
      new XlsReporter<Movement>("R", Descriptors(quantityFormat: "#,##0.00"), styles), data);

    var formatted = sheet.GetRow(1).GetCell(1);
    Assert.Equal("#,##0.00", formatted.CellStyle.GetDataFormatString());
    // The format is a variation of the cell's style, not a replacement: the theme font survives.
    Assert.Equal("Calibri", FontOf(formatted).FontName);
    Assert.Equal(1234.5, formatted.NumericCellValue, 5);

    // A column without a format of its own is untouched.
    Assert.Equal("General", sheet.GetRow(1).GetCell(0).CellStyle.GetDataFormatString());
  }

  [Fact]
  public async Task A_column_format_wins_over_the_theme_date_format()
  {
    var descriptors = new DescriptorsBuilder<Movement>()
      .Column("Data", p => p.Date, "dd/MM/yyyy")
      .Build();
    IEnumerable<Movement> data = [new() { Date = new DateTime(2026, 3, 14) }];

    var sheet = await RenderAsync(new XlsReporter<Movement>("R", descriptors), data);
    var cell = sheet.GetRow(1).GetCell(0);

    Assert.Equal("dd/MM/yyyy", cell.CellStyle.GetDataFormatString());
    Assert.Equal(new DateTime(2026, 3, 14), cell.DateCellValue);
  }

  [Fact]
  public async Task The_theme_date_format_applies_where_a_column_says_nothing()
  {
    IEnumerable<Movement> data = [new() { Date = new DateTime(2026, 3, 14) }];

    var styles = new XlsStyleOptions<Movement> { Theme = XlsTheme.Default with { DateFormat = "yyyy-mm-dd" } };
    var sheet = await RenderAsync(new XlsReporter<Movement>("R", Descriptors(), styles), data);

    Assert.Equal("yyyy-mm-dd", sheet.GetRow(1).GetCell(2).CellStyle.GetDataFormatString());
  }

  [Fact]
  public async Task PostProcess_runs_against_the_finished_sheet()
  {
    var styles = new XlsStyleOptions<Movement>
    {
      // Deliberately after AutoSizeColumn: an explicit width is meant to win over it.
      PostProcess = (_, sheet) =>
      {
        sheet.SetColumnWidth(0, 10000);
        sheet.CreateFreezePane(0, 1);
      }
    };
    IEnumerable<Movement> data = [new() { Code = "A" }];

    var sheet = await RenderAsync(new XlsReporter<Movement>("R", Descriptors(), styles), data);

    Assert.Equal(10000, sheet.GetColumnWidth(0));
  }

  [Fact]
  public async Task Options_are_reusable_across_renders()
  {
    // A style belongs to the workbook that made it, so the options may only hold recipes. Two
    // renders from one options object must both succeed and look the same.
    var styles = new XlsStyleOptions<Movement> { Theme = XlsTheme.Default with { FontFamily = "Calibri" } };
    IEnumerable<Movement> data = [new() { Code = "A" }];

    var first = await RenderAsync(new XlsReporter<Movement>("R", Descriptors(), styles), data);
    var second = await RenderAsync(new XlsReporter<Movement>("R", Descriptors(), styles), data);

    Assert.Equal("Calibri", FontOf(first.GetRow(0).GetCell(0)).FontName);
    Assert.Equal("Calibri", FontOf(second.GetRow(0).GetCell(0)).FontName);
  }

  private sealed class GroupingReporter(
    string title,
    IEnumerable<ColumnDescriptor<Movement>> descriptors,
    XlsStyleOptions<Movement>? styles)
    : XlsGroupingReporter<Movement>(title, descriptors, [], styles);

  [Fact]
  public async Task The_grouping_reporter_is_styled_by_the_same_options()
  {
    // Two value columns and two rows in the group: the merged label regions this reporter draws
    // have to span more than one cell, which NPOI refuses.
    var descriptors = new DescriptorsBuilder<Movement>()
      .Group("Codice", p => p.Code)
      .Column("Quantita", p => p.Quantity)
      .Column("Data", p => p.Date)
      .Build();
    var styles = new XlsStyleOptions<Movement> { Theme = XlsTheme.Default with { FontFamily = "Calibri" } };
    IEnumerable<Movement> data = [new() { Code = "A", Quantity = 1m }, new() { Code = "A", Quantity = 2m }];

    var sheet = await RenderAsync(new GroupingReporter("R", descriptors, styles), data);

    Assert.Equal("Calibri", FontOf(sheet.GetRow(0).GetCell(0)).FontName);
    // ...and the data cells the group holds, which take the same theme through the four-argument
    // GetDataStyle this reporter adds.
    Assert.Equal("Calibri", FontOf(sheet.GetRow(1).GetCell(1)).FontName);
  }
}
