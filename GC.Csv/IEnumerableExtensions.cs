using System.Text;

namespace GC.Csv;

public static class IEnumerableExtensions
{
  public static string ToCsvString<T>(this IEnumerable<T> ext, Action<DataParserBuilder<T>> config)
  {
    var builder = new DataParserBuilder<T>();
    config.Invoke(builder);
    var parser = builder.Build();

    StringBuilder sb = new();

    sb.AppendLine(parser.WriteHeaders());

    foreach (var item in ext)
    {
      sb.AppendLine(parser.WriteRow(item));
    }

    return sb.ToString();
  }
}
