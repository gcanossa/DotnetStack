

using System.Globalization;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace GKit.Reporting;

/// <summary>
/// Writes a flat sheet: one header row, one row per item.
/// </summary>
/// <remarks>
/// Styling has two seams. <paramref name="styles"/> customises a report without a subclass — a
/// <see cref="XlsTheme"/> for the sheet's look and <see cref="XlsStyleOptions{T}.Resolve"/> for the
/// individual cells that differ — and is what the grid export and any caller that only wants a
/// different font should reach for. The <c>Get*Style</c> methods remain virtual for a reporter that
/// is a type of its own; a resolver takes precedence over them.
/// </remarks>
public class XlsReporter<T>(
  string title,
  IEnumerable<ColumnDescriptor<T>> descriptors,
  XlsStyleOptions<T>? styles = null) : IReporter<T>
{
  protected readonly Dictionary<string, ICellStyle> _stylesCache = [];

  /// <summary>How this report is styled. Never null; defaults to the unmodified GKit look.</summary>
  protected XlsStyleOptions<T> Styles { get; } = styles ?? XlsStyleOptions<T>.Default;

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

  /// <summary>
  /// The style <see cref="Styles"/>' theme gives a role, built once per workbook.
  /// </summary>
  protected ICellStyle ThemeStyle(IWorkbook workbook, XlsCellRole role)
  {
    return MemoCellStyle($"theme:{role}", () =>
    {
      var theme = Styles.Theme;
      var header = role == XlsCellRole.Header;

      var font = workbook.CreateFont().FontStyle(theme.FontFamily, theme.FontSize);
      if (header && theme.HeaderBold) font.Bold();
      if (header && theme.HeaderFontColor is { } fontColor) font.Color(fontColor);

      var style = workbook.CreateCellStyle()
        .WithFont(font)
        .VerticalAlign(theme.VerticalAlignment)
        .BorderStyle(theme.Border);

      if (theme.BorderColor is { } borderColor) style.BorderColor(borderColor);
      if (theme.HorizontalAlignment is { } horizontal) style.Alignment = horizontal;
      if (header && theme.HeaderBackground is { } background) style.WithBackground(background);

      // A date needs a number format, otherwise Excel shows the underlying serial number.
      if (role == XlsCellRole.Date)
        style.DataFormat = workbook.CreateDataFormat().GetFormat(theme.DateFormat);

      return style;
    });
  }

  /// <summary>
  /// The one place a cell's style is decided: the resolver first, then the role's default, then
  /// the column's own number format over whichever of the two answered.
  /// </summary>
  protected virtual ICellStyle StyleFor(IWorkbook workbook, XlsCellContext<T> context)
  {
    var resolved = Styles.Resolve?.Invoke(context);

    var style = resolved is null
      ? DefaultStyleFor(workbook, context)
      : MemoCellStyle(resolved.Key, () => resolved.Create(workbook));

    return context.Column?.Format is { } format ? WithFormat(workbook, style, format) : style;
  }

  /// <summary>
  /// The style a cell has when <see cref="XlsStyleOptions{T}.Resolve"/> declines it.
  /// </summary>
  /// <remarks>
  /// The point a subclass overrides rather than <see cref="StyleFor"/>, so it never has to think
  /// about the resolver taking precedence — or about the column format applied over the result.
  /// </remarks>
  protected virtual ICellStyle DefaultStyleFor(IWorkbook workbook, XlsCellContext<T> context) => context.Role switch
  {
    XlsCellRole.Header => GetHeaderStyle(workbook),
    XlsCellRole.Date => GetDateStyle(workbook),
    _ => GetDataStyle(workbook)
  };

  /// <summary>
  /// A column's number format is a variation of a style, not a style of its own: the cell keeps
  /// the font and borders it would have had and gains the format on a copy.
  /// </summary>
  /// <remarks>
  /// Keyed on the source style's own index, so a subclass handing out a different base style per
  /// row gets a formatted copy of each rather than the first one for all of them.
  /// </remarks>
  private ICellStyle WithFormat(IWorkbook workbook, ICellStyle style, string format) =>
    MemoCellStyle($"format:{style.Index}:{format}", () =>
    {
      var formatted = workbook.CloneStyle(style);
      formatted.DataFormat = workbook.CreateDataFormat().GetFormat(format);

      return formatted;
    });

  protected virtual ICellStyle GetHeaderStyle(IWorkbook workbook) => ThemeStyle(workbook, XlsCellRole.Header);

  protected virtual ICellStyle GetDataStyle(IWorkbook workbook) => ThemeStyle(workbook, XlsCellRole.Data);

  protected virtual ICellStyle GetDateStyle(IWorkbook workbook) => ThemeStyle(workbook, XlsCellRole.Date);

  /// <summary>Whether a value is written as a date, and so belongs to <see cref="XlsCellRole.Date"/>.</summary>
  protected static bool IsDate(object? value) => value is DateTime or DateTimeOffset or DateOnly;

  /// <summary>
  /// Writes a value using its native cell type.
  /// <para>
  /// Formatting everything through ToString() made numbers and dates arrive in Excel as text:
  /// not summable, not sortable, and rendered in whatever culture the *server* happened to run
  /// under. A typed cell holds the raw value and lets Excel present it in the reader's locale,
  /// which removes the culture question entirely.
  /// </para>
  /// </summary>
  /// <remarks>
  /// Writes the value only. A date's number format arrives with its style, chosen by
  /// <see cref="StyleFor"/> before this is called — styling a cell here would be the one place
  /// that escaped <see cref="XlsStyleOptions{T}.Resolve"/>.
  /// </remarks>
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
        break;
      case DateTimeOffset dto:
        cell.SetCellValue(dto.DateTime);
        break;
      case DateOnly d:
        cell.SetCellValue(d.ToDateTime(TimeOnly.MinValue));
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

    var rowIdx = 0;
    var colIdx = 0;

    var row = sheet.CreateRow(rowIdx);
    foreach (var col in descriptors)
    {
      var label = col.Label ?? $"Colonna {colIdx}";

      row.CreateCell(colIdx)
        .WithStyle(StyleFor(workbook,
          new XlsCellContext<T>(XlsCellRole.Header, col, colIdx, rowIdx, default, label)))
        .SetCellValue(label);
      colIdx++;
    }

    foreach (var datum in data)
    {
      rowIdx++;
      row = sheet.CreateRow(rowIdx);
      colIdx = 0;
      foreach (var col in descriptors)
      {
        var value = col.SelectValue(datum);
        var role = IsDate(value) ? XlsCellRole.Date : XlsCellRole.Data;

        var cell = row.CreateCell(colIdx)
          .WithStyle(StyleFor(workbook, new XlsCellContext<T>(role, col, colIdx, rowIdx, datum, value)));

        SetCellValue(cell, value, workbook);
        colIdx++;
      }
    }

    colIdx = 0;
    foreach (var col in descriptors)
    {
      sheet.AutoSizeColumn(colIdx);
      colIdx++;
    }

    // After auto-sizing: an explicit column width set here is meant to win over it.
    Styles.PostProcess?.Invoke(workbook, sheet);

    workbook.Write(output, true);

    await Task.CompletedTask;
  }
}
