namespace GKit.UI;

/// <summary>
/// A single sort instruction, expressed as a dotted property path so it carries no
/// dependency on any component library's sort model.
/// </summary>
public sealed record GridSort(string Path, bool Descending);

/// <summary>
/// The intersection of the filter operators offered by the supported component libraries.
/// </summary>
public enum GridFilterOperator
{
  /// <summary>
  /// A library-specific operator with no neutral equivalent. The original is preserved in
  /// <see cref="GridFilter.RawOperator"/>, and filtering itself is unaffected - it runs through
  /// <see cref="GridFilterSet{T}.Native"/>, not through this enum.
  /// </summary>
  Unknown,

  Contains,
  NotContains,
  Equal,
  NotEqual,
  StartsWith,
  EndsWith,
  GreaterThan,
  GreaterThanOrEqual,
  LessThan,
  LessThanOrEqual,
  Empty,
  NotEmpty,
  IsNull,
  IsNotNull
}

/// <summary>
/// A single filter instruction in neutral form. Adapters project their native filter
/// definitions into this so application code can inspect them without referencing the
/// underlying component library.
/// </summary>
/// <remarks>
/// This is a projection for inspection, logging and serialisation. It is deliberately not what
/// applies the filter - see <see cref="GridFilterSet{T}"/>.
/// </remarks>
public sealed record GridFilter(string Path, GridFilterOperator Operator, object? Value, Type? PropertyType = null)
{
  /// <summary>
  /// The originating library's operator token, kept so nothing is lost when
  /// <see cref="Operator"/> is <see cref="GridFilterOperator.Unknown"/>.
  /// </summary>
  public string? RawOperator { get; init; }
}

/// <summary>
/// Carries the filters currently applied to a grid, together with the ability to apply them
/// to a query.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Native"/> delegate is the mechanism that keeps this type honest. Rather than
/// reimplementing each component library's filter translation - which could never be faithful to
/// both - the adapter captures its own library's filter application as a closure and hands it
/// over. <see cref="Apply"/> invokes it without knowing what is inside.
/// </para>
/// <para>
/// <see cref="Filters"/> is the neutral projection, provided so callers can inspect, log or
/// serialise what is applied. It is not what <see cref="Apply"/> uses.
/// </para>
/// </remarks>
public sealed class GridFilterSet<T>
{
  public IReadOnlyList<GridFilter> Filters { get; init; } = [];

  /// <summary>
  /// The adapter-supplied filter application. Null means "no filtering".
  /// </summary>
  public Func<IQueryable<T>, IQueryable<T>>? Native { get; init; }

  public IQueryable<T> Apply(IQueryable<T> source) => Native?.Invoke(source) ?? source;

  public static GridFilterSet<T> Empty { get; } = new();
}

/// <summary>
/// The state of a grid at the moment it asks for data: paging window, sorting and filtering.
/// Replaces the component-library-specific grid state types.
/// </summary>
public sealed class GridQuery<T>
{
  public int StartIndex { get; init; }
  public int Count { get; init; }

  public GridFilterSet<T> Filters { get; init; } = GridFilterSet<T>.Empty;
  public IReadOnlyList<GridSort> Sorts { get; init; } = [];

  /// <summary>
  /// The adapter-supplied sort application. Null means the caller should fall back to
  /// ordering by <see cref="Sorts"/> (see QueryOrderExtensions in GKit.UI.Data).
  /// </summary>
  public Func<IQueryable<T>, IQueryable<T>>? NativeSort { get; init; }

  public IQueryable<T> ApplyFilters(IQueryable<T> source) => Filters.Apply(source);

  public IQueryable<T> ApplySorts(IQueryable<T> source) => NativeSort?.Invoke(source) ?? source;

  public IQueryable<T> Apply(IQueryable<T> source) => ApplySorts(ApplyFilters(source));
}

/// <summary>
/// One page of grid data plus the unpaged total, as returned to a virtualised grid.
/// </summary>
public sealed class GridPage<T>
{
  public IReadOnlyList<T> Items { get; init; } = [];
  public int TotalItems { get; init; }

  public static GridPage<T> Empty { get; } = new();
}
