using System.Reflection;
using GKit.Reporting;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace GKit.MudBlazorExt;

public static class MudDataGridExtensions
{
  /// <summary>
  /// Resolves a column's <c>PropertyName</c> ("Customer.Name") to the chain of properties to
  /// read. Returns null when the column is not a plain property chain — a computed expression
  /// such as <c>x =&gt; x.Lines.Count</c> produces a PropertyName that cannot be resolved, and
  /// the previous <c>GetProperty(cur)!</c> then NRE'd inside the aggregate.
  /// </summary>
  private static List<PropertyInfo>? ResolveChain<T>(string? propertyName)
  {
    if (string.IsNullOrWhiteSpace(propertyName)) return null;

    var chain = new List<PropertyInfo>();
    var current = typeof(T);

    foreach (var part in propertyName.Split('.'))
    {
      var property = current.GetProperty(part);
      if (property is null) return null;

      chain.Add(property);
      current = property.PropertyType;
    }

    return chain;
  }

  public static async Task ToXlsAsync<T>(this IQueryable<T> query, string title, MudDataGrid<T> grid, Stream output)
  {
    var columns = grid.RenderedColumns
      .Where(p => p is not TemplateColumn<T>)
      .Select((column, index) => new
      {
        Column = column,
        Index = index,
        Chain = ResolveChain<T>(column.PropertyName)
      })
      // Skipped rather than fatal: one unresolvable column must not fail the whole export.
      .Where(p => p.Chain is not null)
      .ToList();

    var descriptors = columns.Select(c => new ColumnDescriptor<T, object?>(
      c.Column.Title ?? c.Column.PropertyName ?? $"Colonna {c.Index}",
      item => c.Chain!.Aggregate((object?)item, (acc, prop) => acc is null ? null : prop.GetValue(acc))));

    var reporter = new XlsReporter<T>(title, descriptors);

    var data = query is IAsyncEnumerable<T> ? await query.ToListAsync() : [.. query];

    await reporter.WriteReportAsync(data, output);
  }
}
