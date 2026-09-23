namespace GKit.Reporting;

public abstract class ColumnDescriptor<T>(string label, Func<T, object> selector, string? format = null)
{
  public string Label => label;
  public abstract Type Type { get; }

  /// <summary>
  /// The Excel number format this column's cells carry — <c>"#,##0.00"</c>, <c>"0%"</c>,
  /// <c>"dd/MM/yyyy"</c> — or null to leave the format to the theme.
  /// </summary>
  /// <remarks>
  /// Per column rather than per theme because a theme cannot tell one decimal column holding money
  /// from another holding a quantity. It is an Excel format string, not a .NET one: the cell holds
  /// the raw value and Excel renders it for whoever opens the file, which is what keeps a report
  /// free of the server's culture.
  /// </remarks>
  public string? Format => format;

  public object SelectValue(T item)
  {
    return selector.Invoke(item);
  }
}

public class ColumnDescriptor<T, TProp>(string label, Func<T, TProp> selector, string? format = null)
  : ColumnDescriptor<T>(label, p => selector.Invoke(p)!, format)
{
  public override Type Type => typeof(TProp);
}
