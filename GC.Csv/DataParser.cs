namespace GC.Csv;

public class DataParser<T>
{
  private readonly List<ColumnDescriptor<T>> _descriptors;

  internal DataParser(List<ColumnDescriptor<T>> descriptors)
  {
    _descriptors = descriptors;
  }
  public string WriteHeaders()
  {
    return string.Join(",", _descriptors.Select(p => $"\"{p.Title}\""));
  }

  public string WriteRow(T row)
  {
    return string.Join(",", _descriptors.Select(p => $"\"{p.Selector(row)}\""));
  }
}