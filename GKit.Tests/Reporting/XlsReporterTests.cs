using GKit.Reporting;
using GKit.Tests.Infrastructure;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace GKit.Tests.Reporting;

public class XlsReporterTests
{
  public class Movement
  {
    public string Code { get; set; } = "";
    public decimal Quantity { get; set; }
    public DateTime Date { get; set; }
  }

  private static IEnumerable<ColumnDescriptor<Movement>> Descriptors() =>
    new DescriptorsBuilder<Movement>()
      .Column("Codice", p => p.Code)
      .Column("Quantita", p => p.Quantity)
      .Column("Data", p => p.Date)
      .Build();

  private static async Task<ISheet> RenderAsync(
    XlsReporter<Movement> reporter, IEnumerable<Movement> data)
  {
    using var ms = new MemoryStream();
    await reporter.WriteReportAsync(data, ms);
    ms.Position = 0;
    return new XSSFWorkbook(ms).GetSheetAt(0);
  }

  [Fact]
  public async Task Writes_a_header_row_and_one_row_per_item()
  {
    IEnumerable<Movement> data = [new() { Code = "A" }, new() { Code = "B" }];

    var sheet = await RenderAsync(new XlsReporter<Movement>("Report", Descriptors()), data);

    Assert.Equal("Codice", sheet.GetRow(0).GetCell(0).StringCellValue);
    Assert.Equal(2, sheet.LastRowNum);   // rows 0..2 => header + 2 data rows
    Assert.Equal("A", sheet.GetRow(1).GetCell(0).StringCellValue);
    Assert.Equal("B", sheet.GetRow(2).GetCell(0).StringCellValue);
  }

  [Fact]
  public async Task Numeric_columns_are_written_as_numbers()
  {
    // Every value goes through ToString(), so quantities land in Excel as text: no SUM(), no
    // numeric sort, and the decimal separator follows the server's culture.
    IEnumerable<Movement> data = [new() { Code = "A", Quantity = 12.5m }];

    var sheet = await RenderAsync(new XlsReporter<Movement>("Report", Descriptors()), data);
    var cell = sheet.GetRow(1).GetCell(1);

    Assert.Equal(CellType.Numeric, cell.CellType);
    Assert.Equal(12.5, cell.NumericCellValue, 5);
  }

  [Fact]
  public async Task Date_columns_are_written_as_dates()
  {
    IEnumerable<Movement> data = [new() { Code = "A", Date = new DateTime(2026, 3, 14) }];

    var sheet = await RenderAsync(new XlsReporter<Movement>("Report", Descriptors()), data);
    var cell = sheet.GetRow(1).GetCell(2);

    Assert.Equal(CellType.Numeric, cell.CellType);
    Assert.Equal(new DateTime(2026, 3, 14), cell.DateCellValue);
  }

  [Fact]
  public async Task Numbers_do_not_depend_on_the_ambient_culture()
  {
    // The stored value must be identical regardless of the server's locale. Asserting on the
    // numeric value rather than a rendered string is the point: a typed cell carries the raw
    // number and Excel formats it for whoever opens the file.
    IEnumerable<Movement> data = [new() { Quantity = 1.5m }];

    double itIt, enUs;
    using (new CultureScope("it-IT"))
      itIt = (await RenderAsync(new XlsReporter<Movement>("R", Descriptors()), data))
        .GetRow(1).GetCell(1).NumericCellValue;
    using (new CultureScope("en-US"))
      enUs = (await RenderAsync(new XlsReporter<Movement>("R", Descriptors()), data))
        .GetRow(1).GetCell(1).NumericCellValue;

    Assert.Equal(1.5, itIt, 5);
    Assert.Equal(itIt, enUs, 5);
  }

  [Fact]
  public async Task A_reporter_can_render_twice()
  {
    // _stylesCache holds ICellStyle instances bound to the workbook created inside the first
    // WriteReportAsync call; reusing the reporter applies them to a different workbook.
    var reporter = new XlsReporter<Movement>("Report", Descriptors());
    IEnumerable<Movement> data = [new() { Code = "A" }];

    await RenderAsync(reporter, data);
    var second = await RenderAsync(reporter, data);

    Assert.Equal("A", second.GetRow(1).GetCell(0).StringCellValue);
  }

  [Fact]
  public async Task Null_values_produce_empty_cells_rather_than_throwing()
  {
    var descriptors = new DescriptorsBuilder<Movement>()
      .Column("Nullable", p => (string?)null)
      .Build();

    var sheet = await RenderAsync(new XlsReporter<Movement>("R", descriptors), [new Movement()]);

    Assert.Equal("", sheet.GetRow(1).GetCell(0).StringCellValue);
  }
}
