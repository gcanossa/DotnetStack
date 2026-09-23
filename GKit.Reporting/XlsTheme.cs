using NPOI.SS.UserModel;

namespace GKit.Reporting;

/// <summary>
/// The presentation of a rendered sheet, held as data rather than as overridden methods, so a
/// report can be restyled without a subclass of <see cref="XlsReporter{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// A theme deliberately holds no <see cref="ICellStyle"/>: a style belongs to the workbook that
/// created it — NPOI throws "This Style does not belong to the supplied Workbook" if one leaks
/// into a second render — so a theme meant to be shared between renders, a DI singleton or a
/// static, can only carry the <em>description</em> of a style. <see cref="XlsReporter{T}"/> turns
/// it into styles once per workbook.
/// </para>
/// <para>
/// The defaults reproduce the output GKit reports had before a theme could be supplied, so
/// <see cref="Default"/> is a no-op and every knob is opt-in.
/// </para>
/// </remarks>
public sealed record XlsTheme
{
  /// <summary>The unmodified GKit look: Tahoma 8, bold headers, thin borders.</summary>
  public static XlsTheme Default { get; } = new();

  public string FontFamily { get; init; } = "Tahoma";

  public short FontSize { get; init; } = 8;

  public bool HeaderBold { get; init; } = true;

  /// <summary>The header row's fill, or null to leave it unfilled.</summary>
  public IndexedColors? HeaderBackground { get; init; }

  /// <summary>The header row's font colour, or null for the workbook default.</summary>
  public IndexedColors? HeaderFontColor { get; init; }

  public BorderStyle Border { get; init; } = BorderStyle.Thin;

  /// <summary>The border colour, or null for the workbook default.</summary>
  public IndexedColors? BorderColor { get; init; }

  public VerticalAlignment VerticalAlignment { get; init; } = VerticalAlignment.Center;

  public HorizontalAlignment? HorizontalAlignment { get; init; }

  /// <summary>
  /// The Excel number format given to date cells. A column carrying its own
  /// <see cref="ColumnDescriptor{T}.Format"/> overrides this.
  /// </summary>
  public string DateFormat { get; init; } = "m/d/yy";
}
