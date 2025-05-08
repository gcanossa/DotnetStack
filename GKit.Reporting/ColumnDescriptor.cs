using System.Linq.Expressions;
using System.Reflection;
using NPOI.SS.Formula.Functions;

namespace GKit.Reporting;

public abstract class ColumnDescriptor<T>(string label, Func<T, object> selector)
{
  public string Label => label;
  public abstract Type Type { get; }

  public object SelectValue(T item)
  {
    return selector.Invoke(item);
  }
}

public class ColumnDescriptor<T, TProp>(string label, Func<T, TProp> selector) : ColumnDescriptor<T>(label, p => selector.Invoke(p)!)
{
  public override Type Type => typeof(TProp);
}