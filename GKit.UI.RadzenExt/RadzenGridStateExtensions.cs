using GKit.UI.Data;
using Radzen;
using Radzen.Blazor;

namespace GKit.UI.RadzenExt;

/// <summary>
/// Translates between Radzen's grid model and the neutral one.
/// </summary>
/// <remarks>
/// Mirrors <c>MudGridStateExtensions</c> in the MudBlazor adapter, and takes the same approach:
/// filtering and sorting cross over as delegates calling Radzen's own
/// <see cref="QueryableExtension"/>, so a filtered grid behaves exactly as Radzen intends. The
/// neutral <see cref="GridFilter"/> and <see cref="GridSort"/> records are a lossy projection for
/// inspection only.
/// </remarks>
public static class RadzenGridStateExtensions
{
  private static GridFilterOperator MapOperator(FilterOperator op) => op switch
  {
    FilterOperator.Contains => GridFilterOperator.Contains,
    FilterOperator.DoesNotContain => GridFilterOperator.NotContains,
    FilterOperator.Equals => GridFilterOperator.Equal,
    FilterOperator.NotEquals => GridFilterOperator.NotEqual,
    FilterOperator.StartsWith => GridFilterOperator.StartsWith,
    FilterOperator.EndsWith => GridFilterOperator.EndsWith,
    FilterOperator.GreaterThan => GridFilterOperator.GreaterThan,
    FilterOperator.GreaterThanOrEquals => GridFilterOperator.GreaterThanOrEqual,
    FilterOperator.LessThan => GridFilterOperator.LessThan,
    FilterOperator.LessThanOrEquals => GridFilterOperator.LessThanOrEqual,
    FilterOperator.IsEmpty => GridFilterOperator.Empty,
    FilterOperator.IsNotEmpty => GridFilterOperator.NotEmpty,
    FilterOperator.IsNull => GridFilterOperator.IsNull,
    FilterOperator.IsNotNull => GridFilterOperator.IsNotNull,
    // Custom, In and NotIn have no neutral equivalent.
    _ => GridFilterOperator.Unknown
  };

  private static GridFilter ToNeutral(FilterDescriptor descriptor) =>
    new(
      descriptor.Property ?? string.Empty,
      MapOperator(descriptor.FilterOperator),
      descriptor.FilterValue,
      descriptor.Type)
    {
      RawOperator = descriptor.FilterOperator.ToString()
    };

  /// <summary>
  /// Wraps Radzen's filter descriptors so they can be applied from neutral code.
  /// </summary>
  public static GridFilterSet<T> ToGridFilterSet<T>(
    this IEnumerable<FilterDescriptor>? descriptors,
    LogicalFilterOperator logicalOperator = LogicalFilterOperator.And,
    FilterCaseSensitivity caseSensitivity = FilterCaseSensitivity.Default)
  {
    var materialised = descriptors?.ToList() ?? [];

    return new GridFilterSet<T>
    {
      Filters = [.. materialised.Select(ToNeutral)],
      Native = query => materialised.Count == 0
        ? query
        : query.Where(materialised, logicalOperator, caseSensitivity)
    };
  }

  /// <summary>
  /// Parses Radzen's <c>OrderBy</c> clause (e.g. <c>"Name asc, Quantity desc"</c>) into neutral
  /// sorts. Used for inspection; the actual ordering runs through Radzen's own extension.
  /// </summary>
  private static IReadOnlyList<GridSort> ParseOrderBy(string? orderBy)
  {
    if (string.IsNullOrWhiteSpace(orderBy))
      return [];

    return
    [
      .. orderBy
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(clause =>
        {
          var parts = clause.Split(' ', StringSplitOptions.RemoveEmptyEntries);
          return new GridSort(parts[0], parts.Length > 1 && parts[1].StartsWith("desc", StringComparison.OrdinalIgnoreCase));
        })
    ];
  }

  /// <summary>
  /// Projects a Radzen grid load request onto the neutral query model.
  /// </summary>
  public static GridQuery<T> ToGridQuery<T>(
    this LoadDataArgs args,
    int defaultPageSize = 50,
    LogicalFilterOperator logicalOperator = LogicalFilterOperator.And,
    FilterCaseSensitivity caseSensitivity = FilterCaseSensitivity.Default)
  {
    var orderBy = args.OrderBy;

    return new GridQuery<T>
    {
      StartIndex = args.Skip ?? 0,
      Count = args.Top ?? defaultPageSize,
      Filters = args.Filters.ToGridFilterSet<T>(logicalOperator, caseSensitivity),
      Sorts = ParseOrderBy(orderBy),
      NativeSort = query => string.IsNullOrWhiteSpace(orderBy) ? query : query.OrderBy(orderBy)
    };
  }

  /// <summary>
  /// Projects the grid's columns onto export columns, dropping template columns since they have
  /// no underlying property to read.
  /// </summary>
  public static IReadOnlyList<ExportColumn> ToExportColumns<T>(
    this RadzenDataGrid<T> grid, IGKitUiStrings strings)
    where T : notnull
  {
    return
    [
      .. grid.ColumnsCollection
        .Where(column => !string.IsNullOrEmpty(column.Property))
        .Select((column, index) => new ExportColumn(
          string.IsNullOrEmpty(column.Title) ? strings.ColumnFallback(index) : column.Title,
          column.Property!))
    ];
  }
}
