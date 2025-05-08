

using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace GKit.Reporting;

public class XlsReporter<T>(string title, IEnumerable<ColumnDescriptor<T>> descriptors) : IReporter<T>
{
  protected readonly Dictionary<string, ICellStyle> _stylesCache = [];
  protected virtual ICellStyle MemoCellStyle(string key, Func<ICellStyle> factory)
  {
    if (!_stylesCache.TryGetValue(key, out ICellStyle? value))
    {
      var style = factory.Invoke();
      value = style;
      _stylesCache.Add(key, value);
    }

    return value;
  }

  public virtual ICellStyle GetHeaderStyle(IWorkbook workbook)
  {
    return MemoCellStyle(nameof(GetHeaderStyle), () => workbook.CreateCellStyle()
      .WithFont(workbook.CreateFont().FontStyle("Tahoma", 8).Bold())
      .VerticalAlign(VerticalAlignment.Center)
      .BorderStyle(BorderStyle.Thin));
  }
  public virtual ICellStyle GetDataStyle(IWorkbook workbook)
  {
    return MemoCellStyle(nameof(GetDataStyle), () => workbook.CreateCellStyle()
      .WithFont(workbook.CreateFont().FontStyle("Tahoma", 8))
      .VerticalAlign(VerticalAlignment.Center)
      .BorderStyle(BorderStyle.Thin));
  }

  public virtual async Task WriteReportAsync(IEnumerable<T> data, Stream output)
  {
    XSSFWorkbook workbook = new();

    ISheet sheet = workbook.CreateSheet(title);

    var headerStyle = GetHeaderStyle(workbook);
    var dataStyle = GetDataStyle(workbook);

    var rowIdx = 0;
    var colIdx = 0;

    var row = sheet.CreateRow(rowIdx);
    foreach (var col in descriptors)
    {
      row.CreateCell(colIdx)
        .WithStyle(headerStyle)
        .SetCellValue(col.Label ?? $"Colonna {colIdx}");
      colIdx++;
    }

    foreach (var datum in data)
    {
      rowIdx++;
      row = sheet.CreateRow(rowIdx);
      colIdx = 0;
      foreach (var col in descriptors)
      {
        row.CreateCell(colIdx)
          .WithStyle(dataStyle)
          .SetCellValue(col.SelectValue(datum)?.ToString());
        colIdx++;
      }
    }

    colIdx = 0;
    foreach (var col in descriptors)
    {
      sheet.AutoSizeColumn(colIdx);
      colIdx++;
    }

    workbook.Write(output, true);

    await Task.CompletedTask;
  }
}