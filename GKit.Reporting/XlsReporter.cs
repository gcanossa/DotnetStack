

using System.Globalization;
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

  /// <summary>
  /// Cached styles belong to the workbook that created them — NPOI throws
  /// "This Style does not belong to the supplied Workbook" if they leak into a second render.
  /// Every WriteReportAsync must start from an empty cache so a reporter is reusable.
  /// </summary>
  protected void ResetStyles() => _stylesCache.Clear();

  protected virtual ICellStyle GetHeaderStyle(IWorkbook workbook)
  {
    return MemoCellStyle(nameof(GetHeaderStyle), () => workbook.CreateCellStyle()
      .WithFont(workbook.CreateFont().FontStyle("Tahoma", 8).Bold())
      .VerticalAlign(VerticalAlignment.Center)
      .BorderStyle(BorderStyle.Thin));
  }
  protected virtual ICellStyle GetDataStyle(IWorkbook workbook)
  {
    return MemoCellStyle(nameof(GetDataStyle), () => workbook.CreateCellStyle()
      .WithFont(workbook.CreateFont().FontStyle("Tahoma", 8))
      .VerticalAlign(VerticalAlignment.Center)
      .BorderStyle(BorderStyle.Thin));
  }

  protected virtual ICellStyle GetDateStyle(IWorkbook workbook)
  {
    return MemoCellStyle(nameof(GetDateStyle), () =>
    {
      var style = workbook.CreateCellStyle()
        .WithFont(workbook.CreateFont().FontStyle("Tahoma", 8))
        .VerticalAlign(VerticalAlignment.Center)
        .BorderStyle(BorderStyle.Thin);

      // A date needs a number format, otherwise Excel shows the underlying serial number.
      style.DataFormat = workbook.CreateDataFormat().GetFormat("m/d/yy");

      return style;
    });
  }

  /// <summary>
  /// Writes a value using its native cell type.
  /// <para>
  /// Formatting everything through ToString() made numbers and dates arrive in Excel as text:
  /// not summable, not sortable, and rendered in whatever culture the *server* happened to run
  /// under. A typed cell holds the raw value and lets Excel present it in the reader's locale,
  /// which removes the culture question entirely.
  /// </para>
  /// </summary>
  protected virtual void SetCellValue(ICell cell, object? value, IWorkbook workbook)
  {
    switch (value)
    {
      case null:
        cell.SetCellValue("");
        break;
      case string s:
        cell.SetCellValue(s);
        break;
      case bool b:
        cell.SetCellValue(b);
        break;
      case DateTime dt:
        cell.SetCellValue(dt);
        cell.CellStyle = GetDateStyle(workbook);
        break;
      case DateTimeOffset dto:
        cell.SetCellValue(dto.DateTime);
        cell.CellStyle = GetDateStyle(workbook);
        break;
      case DateOnly d:
        cell.SetCellValue(d.ToDateTime(TimeOnly.MinValue));
        cell.CellStyle = GetDateStyle(workbook);
        break;
      case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
        cell.SetCellValue(Convert.ToDouble(value, CultureInfo.InvariantCulture));
        break;
      default:
        cell.SetCellValue(InvariantValue.Format(value));
        break;
    }
  }

  public virtual async Task WriteReportAsync(IEnumerable<T> data, Stream output)
  {
    ResetStyles();

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
        SetCellValue(row.CreateCell(colIdx).WithStyle(dataStyle), col.SelectValue(datum), workbook);
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