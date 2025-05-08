using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using GKit.Reporting;
using Microsoft.VisualBasic;

namespace Test.Repo.Csv;

public class CsvTest
{
  [Fact]
  public async Task CreateCsv()
  {
    var itemCount = 10;
    var items = Enumerable.Range(0, itemCount).Select(p => new CsvRow { Num = p, DateNow = DateTime.Now.AddDays(p), Title = $"Title {p}" }).ToList();

    var csv = await items.ToCsvStringAsync([
      new ColumnDescriptor<CsvRow, int>("Number", p => p.Num),
      new ColumnDescriptor<CsvRow, string>("Date", p => p.DateNow.ToString("dd/MM/yyyy HH:mm")),
      new ColumnDescriptor<CsvRow, string>("Tit", p => p.Title)
    ]);

    Assert.Equal(itemCount + 1 + 1, csv.Split("\n").Length);
  }

  private class CsvRow
  {
    public int Num { get; set; }
    public DateTime DateNow { get; set; }
    public string Title { get; set; } = "";
  }
}