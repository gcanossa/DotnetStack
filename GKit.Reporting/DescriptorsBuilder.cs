namespace GKit.Reporting;

public class DescriptorsBuilder<T> where T : class
{
  protected readonly List<ColumnDescriptor<T>> _descriptors = [];

  /// <summary>Adds a value column.</summary>
  /// <param name="label">The column heading.</param>
  /// <param name="selector">Reads the column's value from an item.</param>
  /// <param name="format">
  /// An optional Excel number format for the column's cells. See
  /// <see cref="ColumnDescriptor{T}.Format"/>.
  /// </param>
  public DescriptorsBuilder<T> Column<TProp>(string label, Func<T, TProp> selector, string? format = null)
  {
    _descriptors.Add(new ColumnDescriptor<T, TProp>(label, selector, format));
    return this;
  }
  public DescriptorsBuilder<T> Group<TProp>(string label, Func<T, TProp> selector)
  {
    _descriptors.Add(new GroupingColumnDescriptor<T, TProp>(label, selector));
    return this;
  }

  public IEnumerable<ColumnDescriptor<T>> Build()
  {
    return [.. _descriptors];
  }
}
