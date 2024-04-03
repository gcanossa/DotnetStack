using System.Reflection;

namespace GC.Csv;

public class ColumnDescriptor<T>(string title, Func<T, object> selector)
{
  public string Title { get; init; } = title;
  public Func<T, object> Selector { get; init; } = selector;
}