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
  /// Writes the query to <paramref name="output"/> as XLSX.
  /// </summary>
  /// <remarks>
  /// Property paths are resolved per row by reflection, short-circuiting to null as soon as any
  /// step along the path is null.
  /// </remarks>
  public static async Task ToXlsAsync<T>(
    this IQueryable<T> query,
    string title,
    IEnumerable<ExportColumn> columns,
    Stream output)
  {
    var materialisedColumns = columns.ToList();

    var paths = materialisedColumns.ToDictionary(
      column => column,
      column => column.PropertyPath.Split('.').Aggregate(new List<PropertyInfo>(), (acc, segment) =>
      {
        acc.Add(acc.Count == 0
          ? typeof(T).GetProperty(segment)!
          : acc[^1].PropertyType.GetProperty(segment)!);
        return acc;
      }));

    var descriptors = materialisedColumns.Select(column => new ColumnDescriptor<T, object?>(
      column.Title,
      item => paths[column].Aggregate((object?)item, (acc, property) => acc == null ? null : property.GetValue(acc))));

    var reporter = new XlsReporter<T>(title, descriptors);

    var data = query is IAsyncEnumerable<T> ? await query.ToListAsync() : [.. query];

    await reporter.WriteReportAsync(data, output);
  }
}
