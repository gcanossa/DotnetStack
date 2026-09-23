using NPOI.SS.UserModel;

namespace GKit.Reporting;

/// <summary>What a cell is, which is what its style is chosen by.</summary>
public enum XlsCellRole
{
  /// <summary>A column heading.</summary>
  Header,

  /// <summary>An ordinary value cell.</summary>
  Data,

  /// <summary>A value cell holding a date, which needs a number format of its own.</summary>
  Date,

  /// <summary>A group aggregation, written by <see cref="XlsGroupingReporter{T}"/>.</summary>
  Aggregation,

  /// <summary>A group's merged label region, written by <see cref="XlsGroupingReporter{T}"/>.</summary>
  Region
}

/// <summary>Everything the reporter knows about the cell it is about to style.</summary>
/// <param name="Role">What the cell is.</param>
/// <param name="Column">The column being written, or null for cells that belong to no column.</param>
/// <param name="ColumnIndex">The zero-based column, or -1 where the cell belongs to no column.</param>
/// <param name="RowIndex">The zero-based row, header row included.</param>
/// <param name="Item">The item the row was written from, or default for the header row.</param>
/// <param name="Value">The value about to be written, before conversion to a cell type.</param>
public readonly record struct XlsCellContext<T>(
  XlsCellRole Role,
  ColumnDescriptor<T>? Column,
  int ColumnIndex,
  int RowIndex,
  T? Item,
  object? Value);

/// <summary>
/// A style, named so that the workbook holds one instance of it however many cells wear it.
/// </summary>
/// <param name="Key">
/// The cache key. A resolver returning the same key for a thousand cells creates one style, not a
/// thousand — xlsx caps a workbook at roughly 64k cell styles, which a per-cell style reaches on
/// an export of any size. Two styles that differ must therefore differ in their key, and two that
/// agree must share one.
/// </param>
/// <param name="Create">
/// Builds the style. Called once per workbook, so it may create fonts and data formats freely.
/// </param>
public sealed record XlsCellStyle(string Key, Func<IWorkbook, ICellStyle> Create);

/// <summary>
/// How <see cref="XlsReporter{T}"/> styles what it writes: a <see cref="XlsTheme"/> for the look of
/// the whole sheet, and an optional per-cell <see cref="Resolve"/> for the exceptions.
/// </summary>
/// <remarks>
/// Holds recipes rather than styles — see <see cref="XlsTheme"/> — so one instance is safe to build
/// once and reuse across reports, renders and threads.
/// </remarks>
/// <example>
/// <code>
/// var styles = new XlsStyleOptions&lt;Movement&gt;
/// {
///   Theme = XlsTheme.Default with { FontFamily = "Calibri", FontSize = 10 },
///   Resolve = ctx =&gt; ctx is { Role: XlsCellRole.Data, Value: decimal and &lt; 0 }
///     ? new XlsCellStyle("negative", wb =&gt; wb.CreateCellStyle()
///         .WithFont(wb.CreateFont().FontStyle("Calibri", 10).Color(IndexedColors.Red))
///         .BorderStyle(BorderStyle.Thin))
///     : null
/// };
/// </code>
/// </example>
public sealed class XlsStyleOptions<T>
{
  /// <summary>The unmodified GKit look, and no per-cell overrides.</summary>
  public static XlsStyleOptions<T> Default { get; } = new();

  public XlsTheme Theme { get; init; } = XlsTheme.Default;

  /// <summary>
  /// Chooses a style for one cell, or returns null to keep the one the theme gives it.
  /// </summary>
  /// <remarks>
  /// Runs for every cell of every row, so it is a place to compare a value, not to query a
  /// database. It takes precedence over the <c>Get*Style</c> methods a subclass overrides: a
  /// reporter that is both subclassed and given a resolver answers with the resolver where it
  /// returns something, and with the subclass where it returns null.
  /// </remarks>
  public Func<XlsCellContext<T>, XlsCellStyle?>? Resolve { get; init; }

  /// <summary>
  /// Runs against the finished sheet, after columns are auto-sized and before the workbook is
  /// written — the hook for freeze panes, explicit column widths, autofilters and the like.
  /// </summary>
  public Action<IWorkbook, ISheet>? PostProcess { get; init; }
}
