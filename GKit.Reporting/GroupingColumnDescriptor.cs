namespace GKit.Reporting;


public abstract class GroupingColumnDescriptor<T>(string label, Func<T, object> selector) : ColumnDescriptor<T>(label, selector)
{
}
public class GroupingColumnDescriptor<T, TProp>(string label, Func<T, TProp> selector) : GroupingColumnDescriptor<T>(label, p => selector.Invoke(p)!)
{
  public override Type Type => typeof(TProp);
}
