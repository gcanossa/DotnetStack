using GKit.UI.Data;
using MudBlazor;

namespace GKit.UI.MudBlazorExt;

/// <summary>
/// Translates between MudBlazor's grid model and the neutral one.
/// </summary>
/// <remarks>
/// <para>
/// The translation is deliberately asymmetric. Filtering and sorting are carried across as
/// <em>delegates</em> that call straight back into MudBlazor's own
/// <see cref="QueryFilterExtensions"/> and <see cref="QuerySortExtensions"/>, so a filtered grid
/// produces byte-identical SQL to what it produced before the adapter split. The neutral
/// <see cref="GridFilter"/> and <see cref="GridSort"/> records alongside them are a lossy
/// projection, provided only so application code can inspect what is applied.
/// </para>
/// <para>
/// Reimplementing MudBlazor's filter translation in neutral code would have been the obvious
/// alternative, and would have been wrong: the two libraries do not agree on operator semantics
/// or case sensitivity, and any shared reimplementation would be faithful to neither.
/// </para>
/// </remarks>
public static class MudGridStateExtensions
{
  private static readonly Dictionary<string, GridFilterOperator> _operators = BuildOperatorMap();

  private static Dictionary<string, GridFilterOperator> BuildOperatorMap()
  {
    var map = new Dictionary<string, GridFilterOperator>(StringComparer.OrdinalIgnoreCase);

    // Several MudBlazor operator groups share a token ("is empty" is both a string and a number
    // operator), so duplicates are expected and the first mapping wins.
    void Add(string token, GridFilterOperator op) => map.TryAdd(token, op);

    Add(FilterOperator.String.Contains, GridFilterOperator.Contains);
    Add(FilterOperator.String.NotContains, GridFilterOperator.NotContains);
    Add(FilterOperator.String.Equal, GridFilterOperator.Equal);
    Add(FilterOperator.String.NotEqual, GridFilterOperator.NotEqual);
    Add(FilterOperator.String.StartsWith, GridFilterOperator.StartsWith);
    Add(FilterOperator.String.EndsWith, GridFilterOperator.EndsWith);
    Add(FilterOperator.String.Empty, GridFilterOperator.Empty);
    Add(FilterOperator.String.NotEmpty, GridFilterOperator.NotEmpty);

    Add(FilterOperator.Number.Equal, GridFilterOperator.Equal);
    Add(FilterOperator.Number.NotEqual, GridFilterOperator.NotEqual);
    Add(FilterOperator.Number.GreaterThan, GridFilterOperator.GreaterThan);
    Add(FilterOperator.Number.GreaterThanOrEqual, GridFilterOperator.GreaterThanOrEqual);
    Add(FilterOperator.Number.LessThan, GridFilterOperator.LessThan);
    Add(FilterOperator.Number.LessThanOrEqual, GridFilterOperator.LessThanOrEqual);
    Add(FilterOperator.Number.Empty, GridFilterOperator.Empty);
    Add(FilterOperator.Number.NotEmpty, GridFilterOperator.NotEmpty);

    Add(FilterOperator.Boolean.Is, GridFilterOperator.Equal);

    Add(FilterOperator.Enum.Is, GridFilterOperator.Equal);
    Add(FilterOperator.Enum.IsNot, GridFilterOperator.NotEqual);
    Add(FilterOperator.Enum.Empty, GridFilterOperator.Empty);
    Add(FilterOperator.Enum.NotEmpty, GridFilterOperator.NotEmpty);

    Add(FilterOperator.DateTime.Is, GridFilterOperator.Equal);
    Add(FilterOperator.DateTime.IsNot, GridFilterOperator.NotEqual);
    Add(FilterOperator.DateTime.After, GridFilterOperator.GreaterThan);
    Add(FilterOperator.DateTime.OnOrAfter, GridFilterOperator.GreaterThanOrEqual);
    Add(FilterOperator.DateTime.Before, GridFilterOperator.LessThan);
    Add(FilterOperator.DateTime.OnOrBefore, GridFilterOperator.LessThanOrEqual);
    Add(FilterOperator.DateTime.Empty, GridFilterOperator.Empty);
    Add(FilterOperator.DateTime.NotEmpty, GridFilterOperator.NotEmpty);

    Add(FilterOperator.Guid.Equal, GridFilterOperator.Equal);
    Add(FilterOperator.Guid.NotEqual, GridFilterOperator.NotEqual);

    return map;
  }

  private static GridFilterOperator MapOperator(string? token) =>
    token is not null && _operators.TryGetValue(token, out var op) ? op : GridFilterOperator.Unknown;

  /// <summary>
  /// Walks a dotted path to find its leaf type. MudBlazor keeps the column's resolved property
  /// type internal, so it is recovered by reflection here.
  /// </summary>
  private static Type? ResolvePropertyType<T>(string? path)
  {
    if (string.IsNullOrEmpty(path))
      return null;

    var current = typeof(T);
    foreach (var segment in path.Split('.'))
    {
      var property = current.GetProperty(segment);
      if (property is null)
        return null;

      current = property.PropertyType;
    }

    return current;
  }

  private static GridFilter ToNeutral<T>(IFilterDefinition<T> definition)
  {
    var path = definition.Column?.PropertyName ?? definition.Title ?? string.Empty;

    return new GridFilter(path, MapOperator(definition.Operator), definition.Value, ResolvePropertyType<T>(path))
    {
      RawOperator = definition.Operator
    };
  }

  /// <summary>
  /// Wraps MudBlazor's filter definitions so they can be applied from neutral code.
  /// </summary>
  public static GridFilterSet<T> ToGridFilterSet<T>(this IEnumerable<IFilterDefinition<T>>? definitions)
  {
    var materialised = definitions?.ToList() ?? [];

    return new GridFilterSet<T>
    {
      Filters = [.. materialised.Select(ToNeutral)],
      Native = query => QueryFilterExtensions.Where(query, materialised)
    };
  }

  /// <summary>
  /// Projects a virtualised grid's state onto the neutral query model.
  /// </summary>
  public static GridQuery<T> ToGridQuery<T>(this GridStateVirtualize<T> state)
  {
    var sorts = state.SortDefinitions?.ToList() ?? [];

    return new GridQuery<T>
    {
      StartIndex = state.StartIndex,
      Count = state.Count,
      Filters = state.FilterDefinitions.ToGridFilterSet(),
      Sorts = [.. sorts.Select(s => new GridSort(s.SortBy, s.Descending))],
      NativeSort = query => QuerySortExtensions.OrderBy(query, sorts)
    };
  }

  /// <summary>
  /// Projects a paged grid's state onto the neutral query model.
  /// </summary>
  /// <remarks>
  /// MudBlazor reports paging as page index plus size; the neutral model uses a skip/take window,
  /// which is what both libraries' underlying queries want anyway.
  /// </remarks>
  public static GridQuery<T> ToGridQuery<T>(this GridState<T> state)
  {
    var sorts = state.SortDefinitions?.ToList() ?? [];

    return new GridQuery<T>
    {
      StartIndex = state.Page * state.PageSize,
      Count = state.PageSize,
      Filters = state.FilterDefinitions.ToGridFilterSet(),
      Sorts = [.. sorts.Select(s => new GridSort(s.SortBy, s.Descending))],
      NativeSort = query => QuerySortExtensions.OrderBy(query, sorts)
    };
  }

  /// <summary>
  /// Builds a neutral query representing a grid's current filters and sorts, with no paging.
  /// Used for export, which covers the whole filtered set.
  /// </summary>
  public static GridQuery<T> ToUnpagedGridQuery<T>(this MudDataGrid<T> grid)
  {
    var sorts = grid.SortDefinitions?.Values.ToList() ?? [];

    return new GridQuery<T>
    {
      Filters = grid.FilterDefinitions.ToGridFilterSet(),
      Sorts = [.. sorts.Select(s => new GridSort(s.SortBy, s.Descending))],
      NativeSort = query => QuerySortExtensions.OrderBy(query, sorts)
    };
  }

  /// <summary>Converts a neutral page back into MudBlazor's grid data shape.</summary>
  public static GridData<T> ToGridData<T>(this GridPage<T> page) =>
    new() { Items = page.Items, TotalItems = page.TotalItems };

  /// <summary>
  /// Projects the grid's rendered columns onto export columns, dropping template columns since
  /// they have no underlying property to read.
  /// </summary>
  public static IReadOnlyList<ExportColumn> ToExportColumns<T>(this MudDataGrid<T> grid, IGKitUiStrings strings)
  {
    return
    [
      .. grid.RenderedColumns
        .Where(column => column is not TemplateColumn<T> && !string.IsNullOrEmpty(column.PropertyName))
        .Select((column, index) => new ExportColumn(
          column.Title ?? column.PropertyName ?? strings.ColumnFallback(index),
          column.PropertyName!))
    ];
  }
}
