using System.Reflection;
using GKit.Reporting;
using Microsoft.EntityFrameworkCore;

namespace GKit.UI.Data;

/// <summary>
/// One exported column, described by its heading and a dotted property path.
/// </summary>
/// <remarks>
/// Adapters project their rendered columns onto this, skipping template columns that have no
/// underlying property.
/// </remarks>
/// <param name="Title">The column heading.</param>
/// <param name="PropertyPath">The dotted path to the property the column reads.</param>
/// <param name="Format">
/// An optional Excel number format for the column — <c>"#,##0.00"</c>, <c>"0%"</c>. Adapters leave
/// it null: a grid column's format is a .NET format string and means something else, so a caller
/// that wants one says so explicitly when it builds the list.
/// </param>
public sealed record ExportColumn(string Title, string PropertyPath, string? Format = null);

public static class XlsExportExtensions
{
  /// <summary>
  /// Resolves a dotted path to the chain of properties to read, or null when the path is not a
  /// plain property chain.
  /// </summary>
  /// <remarks>
  /// A column bound to a computed expression such as <c>x =&gt; x.Lines.Count</c> yields a path
  /// that cannot be resolved. Returning null lets the caller skip the column; the previous
  /// <c>GetProperty(segment)!</c> produced a null that only surfaced as an NRE mid-export.
  /// </remarks>
  private static List<PropertyInfo>? ResolveChain<T>(string? propertyPath)
  {
    if (string.IsNullOrWhiteSpace(propertyPath))
      return null;

    var chain = new List<PropertyInfo>();
    var current = typeof(T);

    foreach (var segment in propertyPath.Split('.'))
    {
      var property = current.GetProperty(segment);
      if (property is null)
        return null;

      chain.Add(property);
      current = property.PropertyType;
    }

    return chain;
  }

  /// <summary>Writes the query's rows to <paramref name="output"/> as an xlsx sheet.</summary>
  /// <param name="query">The rows to export.</param>
  /// <param name="title">The sheet name.</param>
  /// <param name="columns">The columns to write, in order.</param>
  /// <param name="output">The stream the workbook is written to.</param>
  /// <param name="styles">
  /// How the sheet is styled. Null keeps the unmodified GKit look; the grids pass what their
  /// <c>ExportStyles</c> parameter, or the registered <see cref="XlsTheme"/>, says.
  /// </param>
  public static async Task ToXlsAsync<T>(
    this IQueryable<T> query,
    string title,
    IEnumerable<ExportColumn> columns,
    Stream output,
    XlsStyleOptions<T>? styles = null)
  {
    var resolved = columns
      .Select(column => new { Column = column, Chain = ResolveChain<T>(column.PropertyPath) })
      // Skipped rather than fatal: one unresolvable column must not fail the whole export.
      .Where(entry => entry.Chain is not null)
      .ToList();

    var descriptors = resolved.Select(entry => new ColumnDescriptor<T, object?>(
      entry.Column.Title,
      item => entry.Chain!.Aggregate((object?)item, (acc, property) => acc is null ? null : property.GetValue(acc)),
      entry.Column.Format));

    var reporter = new XlsReporter<T>(title, descriptors, styles);

    var data = query is IAsyncEnumerable<T> ? await query.ToListAsync() : [.. query];

    await reporter.WriteReportAsync(data, output);
  }
}
