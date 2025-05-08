using System.Text;
using System.Threading.Tasks;

namespace GKit.Reporting;

public static class IEnumerableExtensions
{
  public static async Task<string> ToCsvStringAsync<T>(this IEnumerable<T> ext, IEnumerable<ColumnDescriptor<T>> config)
  {
    var reporter = new CsvReporter<T>(config);

    return await reporter.WriteToStringAsync(ext);
  }
}
