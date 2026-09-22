using System.Text;

namespace GKit.Reporting;

public class CsvReporterOptions
{
  /// <summary>Field separator. Italian Excel defaults to <c>;</c>.</summary>
  public string Separator { get; set; } = ",";

  /// <summary>Line terminator. RFC 4180 specifies CRLF.</summary>
  public string LineTerminator { get; set; } = "\r\n";

  /// <summary>
  /// Prepend a UTF-8 byte order mark. Excel needs it to read a UTF-8 CSV as UTF-8 rather than
  /// the system ANSI code page, which mangles accented characters.
  /// </summary>
  public bool WriteByteOrderMark { get; set; }

  public bool WriteHeaders { get; set; } = true;
}

public class CsvReporter<T> : IReporter<T>
{
  private readonly IEnumerable<ColumnDescriptor<T>> _descriptors;
  private readonly CsvReporterOptions _options;

  public CsvReporter(IEnumerable<ColumnDescriptor<T>> descriptors, CsvReporterOptions? options = null)
  {
    _descriptors = descriptors;
    _options = options ?? new CsvReporterOptions();
  }

  public async Task WriteReportAsync(IEnumerable<T> data, Stream output)
  {
    var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: _options.WriteByteOrderMark);

    // leaveOpen: the caller owns the stream. Disposing the writer is what flushes the encoder;
    // relying on FlushAsync alone could drop the tail of a large report.
    await using var writer = new StreamWriter(output, encoding, leaveOpen: true)
    {
      NewLine = _options.LineTerminator
    };

    if (_options.WriteHeaders)
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

  /// <summary>
  /// Quotes a field per RFC 4180: wrap in double quotes and double any embedded quote.
  /// <para>
  /// Fields were previously interpolated straight between two quote characters, so a value
  /// containing <c>"</c> — routine in Italian free-text descriptions — produced a file no CSV
  /// parser could read, and a value containing a newline or the separator broke the record.
  /// </para>
  /// </summary>
  protected static string Quote(string? value) =>
    $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

  protected string WriteHeaders()
  {
    return string.Join(_options.Separator, _descriptors.Select(p => Quote(p.Label)));
  }

  protected string WriteRow(T row)
  {
    return string.Join(_options.Separator,
      _descriptors.Select(p => Quote(InvariantValue.Format(p.SelectValue(row)))));
  }
}
