using NPOI.SS.Formula.Functions;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace GKit.Reporting;

public abstract class XlsGroupingReporter<T>(string title, IEnumerable<ColumnDescriptor<T>> descriptors, IEnumerable<Func<IEnumerable<T>, object>> aggregations)
  : XlsReporter<T>(title, descriptors.AsEnumerable())
{
  protected override ICellStyle GetHeaderStyle(IWorkbook workbook)
  {
    return MemoCellStyle(nameof(GetHeaderStyle), () => workbook.CreateCellStyle()
      .WithFont(workbook.CreateFont().FontStyle("Tahoma", 8).Bold())
      .VerticalAlign(VerticalAlignment.Center)
      .BorderStyle(BorderStyle.Thin));
  }
  protected virtual ICellStyle GetDataStyle(IWorkbook workbook, T item, int index, ColumnDescriptor<T> column)
  {
    return MemoCellStyle(nameof(GetDataStyle), () => workbook.CreateCellStyle()
      .WithFont(workbook.CreateFont().FontStyle("Tahoma", 8))
      .VerticalAlign(VerticalAlignment.Center)
      .BorderStyle(BorderStyle.Thin));
  }
  protected virtual ICellStyle GetAggregationStyle(IWorkbook workbook)
  {
    return MemoCellStyle(nameof(GetAggregationStyle), () => workbook.CreateCellStyle()
      .WithFont(workbook.CreateFont().FontStyle("Tahoma", 8))
      .VerticalAlign(VerticalAlignment.Center)
      .BorderStyle(BorderStyle.Thin));
  }
  protected virtual ICellStyle GetRegionStyle(IWorkbook workbook)
  {
    return MemoCellStyle(nameof(GetRegionStyle), () => workbook.CreateCellStyle()
      .WithFont(workbook.CreateFont().FontStyle("Tahoma", 8))
      .VerticalAlign(VerticalAlignment.Center)
      .BorderStyle(BorderStyle.Thin));
  }
  protected virtual void ApplyRegionStyle(ISheet sheet, CellRangeAddress range)
  {
    sheet.RegionWithBorderStyle(range, BorderStyle.Thin);
  }

  private void CreateGroupSection(IWorkbook workbook, ISheet sheet, GroupingItem<T> node, int baseRowIndex, int baseColIndex)
  {
    var verticalRange = new CellRangeAddress(
        baseRowIndex,
        baseRowIndex + node.Items.Count() + node.AllSubNodes.Count() * (aggregations.Any() ? 2 : 1) + (aggregations.Any() ? 1 : 0),
        baseColIndex,
        baseColIndex);

    var horizontalRange = new CellRangeAddress(baseRowIndex, baseRowIndex, baseColIndex + 1, baseColIndex + 1 + node.Depth - 1 + descriptors.Count() - 1);

    sheet.GetRow(verticalRange.FirstRow).GetCell(verticalRange.FirstColumn).SetCellValue("-" ?? "").WithStyle(GetRegionStyle(workbook));
    sheet.GetRow(horizontalRange.FirstRow).GetCell(horizontalRange.FirstColumn).SetCellValue($"{node.Label}: {node.Value} (tot = {node.Items.Count()})");

    ApplyRegionStyle(sheet, verticalRange);
    ApplyRegionStyle(sheet, horizontalRange);

    sheet.AddMergedRegion(verticalRange);
    sheet.AddMergedRegion(horizontalRange);

    if (node.Children.Any())
    {
      CreateGroupSections(workbook, sheet, node.Children, baseRowIndex + 1, baseColIndex + 1);
    }
    else
    {
      var row = 0;
      int col;
      foreach (var item in node.Items)
      {
        col = 0;
        foreach (var prop in descriptors)
        {
          var cell = sheet.GetRow(baseRowIndex + 1 + row).GetCell(baseColIndex + 1 + col++).SetCellValue(prop.SelectValue(item)?.ToString() ?? "");
          cell.CellStyle = GetDataStyle(workbook, item, row, prop);
        }
        row++;
      }
    }

    var lastRow = node.Items.Count() + node.AllSubNodes.Count() * 2;
    var firstCol = node.Depth - 1;
    foreach (var agg in aggregations)
    {
      var cell = sheet.GetRow(baseRowIndex + 1 + lastRow).GetCell(baseColIndex + 1 + firstCol++).SetCellValue(agg.Invoke(node.Items)?.ToString() ?? "");
      cell.CellStyle = GetAggregationStyle(workbook);
    }
  }

  private void CreateGroupSections(IWorkbook workbook, ISheet sheet, IEnumerable<GroupingItem<T>> groups, int baseRowIndex, int baseColIndex)
  {
    var children = groups.ToList();
    for (var i = 0; i < children.Count; i++)
    {
      CreateGroupSection(
        workbook,
        sheet,
        children[i],
        baseRowIndex + Enumerable.Range(0, i)
          .Select(j =>
            children[j].Items.Count() +
            children[j].AllSubNodes.Count() * (aggregations.Any() ? 2 : 1) +
            (aggregations.Any() ? 2 : 1)).Sum(),
        baseColIndex);
    }
  }


  public override async Task WriteReportAsync(IEnumerable<T> data, Stream output)
  {
    XSSFWorkbook workbook = new();

    ISheet sheet = workbook.CreateSheet("Report");

    var headerRow = sheet.CreateRow(0);
    headerRow.HeightInPoints = 40;

    var groupDefs = descriptors.Where(p => p is GroupingColumnDescriptor<T>)
      .Cast<GroupingColumnDescriptor<T>>();
    var propDefs = descriptors.Where(p => p is not GroupingColumnDescriptor<T>);

    var headerCell = 0;
    foreach (var grp in groupDefs)
    {
      var cell = headerRow.CreateCell(headerCell++).SetCellValue(grp.Label);
      cell.CellStyle = GetHeaderStyle(workbook);
    }

    foreach (var prop in propDefs)
    {
      var cell = headerRow.CreateCell(headerCell++).SetCellValue(prop.Label);
      cell.CellStyle = GetHeaderStyle(workbook);
    }

    int rowOffset = 1;
    int colOffset = 0;
    int groupColumns = groupDefs.Count();
    int propertiesColumns = propDefs.Count();
    int totalColumns = groupColumns + propertiesColumns;

    var groupings = groupDefs.ExecuteGrouping(data);

    int totalRows = groupings.Select(p =>
      p.Items.Count() +
      p.AllSubNodes.Count() * (aggregations.Any() ? 2 : 1) +
      (aggregations.Any() ? 2 : 1)).Sum();

    for (int i = 0; i < totalRows; i++)
    {
      var row = sheet.CreateRow(i + rowOffset);
      for (int j = 0; j < totalColumns; j++)
      {
        row.CreateCell(j + colOffset);
      }
    }

    CreateGroupSections(workbook, sheet, groupings, rowOffset, colOffset);

    for (int i = 0; i < totalColumns; i++)
    {
      sheet.AutoSizeColumn(i);
    }

    workbook.Write(output, true);

    await Task.CompletedTask;
  }
}
