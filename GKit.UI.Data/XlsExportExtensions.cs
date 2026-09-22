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
public sealed record ExportColumn(string Title, string PropertyPath);

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

  public static async Task ToXlsAsync<T>(
    this IQueryable<T> query,
    string title,
    IEnumerable<ExportColumn> columns,
    Stream output)
  {
    var resolved = columns
      .Select(column => new { Column = column, Chain = ResolveChain<T>(column.PropertyPath) })
      // Skipped rather than fatal: one unresolvable column must not fail the whole export.
      .Where(entry => entry.Chain is not null)
      .ToList();

    var descriptors = resolved.Select(entry => new ColumnDescriptor<T, object?>(
      entry.Column.Title,
      item => entry.Chain!.Aggregate((object?)item, (acc, property) => acc is null ? null : property.GetValue(acc))));

    var reporter = new XlsReporter<T>(title, descriptors);

    var data = query is IAsyncEnumerable<T> ? await query.ToListAsync() : [.. query];

    await reporter.WriteReportAsync(data, output);
  }
}
