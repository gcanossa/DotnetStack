using System.ComponentModel.DataAnnotations;
using GC.Csv;
using Microsoft.VisualBasic;

namespace Test.Repo.Csv;

public class CsvTest
{
  [Fact]
  public void CreateCsv()
  {
    var itemCount = 10;
    var items = Enumerable.Range(0, itemCount).Select(p => new CsvRow { Num = p, DateNow = DateTime.Now.AddDays(p), Title = $"Title {p}" }).ToList();

    var csv = items.ToCsvString(b =>
    b.AddColumn("Number", p => p.Num)
    .AddColumn("Date", p => p.DateNow.ToString("dd/MM/yyyy HH:mm"))
    .AddColumn("Tit", p => p.Title));

    Assert.Equal(itemCount + 1 + 1, csv.Split("\n").Length);
  }

  private class CsvRow
  {
    public int Num { get; set; }
    public DateTime DateNow { get; set; }
    public string Title { get; set; }
  }
}