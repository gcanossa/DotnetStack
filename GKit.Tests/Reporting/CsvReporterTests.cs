using GKit.Reporting;
using GKit.Tests.Infrastructure;

namespace GKit.Tests.Reporting;

public class CsvReporterTests
{
  public class Row
  {
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
  }

  private static IEnumerable<ColumnDescriptor<Row>> Descriptors() =>
    new DescriptorsBuilder<Row>()
      .Column("Descrizione", p => p.Description)
      .Column("Importo", p => p.Amount)
      .Build();

  [Fact]
  public async Task Writes_a_header_row_from_the_labels()
  {
    var csv = await new CsvReporter<Row>(Descriptors()).WriteToStringAsync([]);

    Assert.StartsWith("\"Descrizione\",\"Importo\"", csv);
  }

  [Fact]
  public async Task Writes_one_line_per_item()
  {
    IEnumerable<Row> data = [new() { Description = "a" }, new() { Description = "b" }];

    var csv = await new CsvReporter<Row>(Descriptors()).WriteToStringAsync(data);
    var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    Assert.Equal(3, lines.Length);
  }

  [Fact]
  public async Task Escapes_embedded_double_quotes()
  {
    // RFC 4180: a quote inside a quoted field is doubled. Italian free-text description fields
    // regularly contain quotes ("Rifiuto \"misto\" da demolizione"), which currently produce a
    // file no CSV parser can read.
    IEnumerable<Row> data = [new() { Description = "Rifiuto \"misto\"" }];

    var csv = await new CsvReporter<Row>(Descriptors()).WriteToStringAsync(data);

    Assert.Contains("\"Rifiuto \"\"misto\"\"\"", csv);
  }

  [Fact]
  public async Task Fields_containing_the_separator_stay_in_one_field()
  {
    // "Rossi, Mario" must not be split across two columns. Quoting alone handles this — but only
    // if the surrounding quotes are the *only* unescaped quotes in the field.
    IEnumerable<Row> data = [new() { Description = "Rossi, Mario \"detto Bigio\"", Amount = 1 }];

    var csv = await new CsvReporter<Row>(Descriptors()).WriteToStringAsync(data);
    var record = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[1];

    // Every quote must be either a field delimiter or part of an escaped "" pair; with correct
    // escaping the record has exactly 4 delimiter quotes plus 4 from the doubled inner pair.
    Assert.Equal(8, record.Count(c => c == '"'));
  }

  [Fact]
  public async Task Numbers_are_written_with_an_invariant_decimal_separator()
  {
    // A CSV produced on an it-IT server is consumed by machines elsewhere; "1,5" inside a
    // comma-separated file is unparseable regardless.
    IEnumerable<Row> data = [new() { Amount = 1.5m }];

    using (new CultureScope("it-IT"))
    {
      var csv = await new CsvReporter<Row>(Descriptors()).WriteToStringAsync(data);
      Assert.Contains("1.5", csv);
    }
  }

  [Fact]
  public async Task Writes_the_full_content_to_the_supplied_stream()
  {
    // WriteReportAsync never disposes its StreamWriter; only an explicit FlushAsync saves it.
    IEnumerable<Row> data = Enumerable.Range(0, 500).Select(i => new Row { Description = $"row-{i}" });

    using var ms = new MemoryStream();
    await new CsvReporter<Row>(Descriptors()).WriteReportAsync(data, ms);

    ms.Position = 0;
    using var reader = new StreamReader(ms);
    var text = await reader.ReadToEndAsync();

    Assert.Contains("row-499", text);
  }
}
