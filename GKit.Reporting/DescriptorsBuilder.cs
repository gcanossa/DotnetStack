namespace GKit.Reporting;

public class DescriptorsBuilder<T> where T : class
{
  protected readonly List<ColumnDescriptor<T>> _descriptors = [];

  public DescriptorsBuilder<T> Column<TProp>(string label, Func<T, TProp> selector)
  {
    _descriptors.Add(new ColumnDescriptor<T, TProp>(label, selector));
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