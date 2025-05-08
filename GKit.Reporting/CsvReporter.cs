

namespace GKit.Reporting;

public class CsvReporter<T>(IEnumerable<ColumnDescriptor<T>> descriptors) : IReporter<T>
{
  public async Task WriteReportAsync(IEnumerable<T> data, Stream output)
  {
    var writer = new StreamWriter(output);

    await writer.WriteLineAsync(WriteHeaders());

    foreach (var item in data)
    {
      await writer.WriteLineAsync(WriteRow(item));
    }

    await writer.FlushAsync();
  }

  public async Task<string> WriteToStringAsync(IEnumerable<T> data)
  {

    using var ms = new MemoryStream();

    await WriteReportAsync(data, ms);

    ms.Position = 0;

    using var reader = new StreamReader(ms);

    return await reader.ReadToEndAsync();
  }

  protected string WriteHeaders()
  {
    return string.Join(",", descriptors.Select(p => $"\"{p.Label}\""));
  }
  protected string WriteRow(T row)
  {
    return string.Join(",", descriptors.Select(p => $"\"{p.SelectValue(row)}\""));
  }
}