using System.Reflection;
using GKit.Reporting;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace GKit.MudBlazorExt;

public static class MudDataGridExtensions
{
  public static async Task ToXlsAsync<T>(this IQueryable<T> query, string title, MudDataGrid<T> grid, Stream output)
  {
    var columns = grid.RenderedColumns.Where(p => p is not TemplateColumn<T>);

    var properties = columns.ToDictionary(p => p, p => p.PropertyName.Split(".").Aggregate(new List<PropertyInfo>(), (acc, cur) =>
    {
      if (acc.Count == 0)
        acc.Add(typeof(T).GetProperty(cur)!);
      else
        acc.Add(acc.Last().PropertyType.GetProperty(cur)!);
      return acc;
    }));

    var descriptors = columns.Select((col, idx) => new ColumnDescriptor<T, object>(
      col.Title ?? col.PropertyName ?? $"Colonna {idx}",
      p => properties[col].Aggregate((object?)p, (acc, prop) => prop.GetValue(acc))!));

    var reporter = new XlsReporter<T>(title, descriptors);

    var data = query is IAsyncEnumerable<T> ? await query.ToListAsync() : [.. query];

    await reporter.WriteReportAsync(data, output);
  }
}
