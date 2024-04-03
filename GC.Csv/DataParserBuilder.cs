namespace GC.Csv;

public class DataParserBuilder<T>
{
  private List<ColumnDescriptor<T>> _columns = new();

  public DataParserBuilder<T> AddColumn(string title, Func<T, object> selector)
  {
    _columns.Add(new(title, selector));

    return this;
  }

  public DataParser<T> Build()
  {
    return new(_columns);
  }
}