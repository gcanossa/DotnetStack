using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace GKit.Reporting;

public abstract class XlsGroupingReporter<T> : XlsReporter<T>
{
  protected IEnumerable<GroupingColumnDescriptor<T>> GroupDefs { get; init; }
  protected IEnumerable<ColumnDescriptor<T>> PropDefs { get; init; }
  protected IEnumerable<Func<IEnumerable<T>, object>> Aggregations { get; init; }

  public XlsGroupingReporter(
    string title,
    IEnumerable<ColumnDescriptor<T>> descriptors,
    IEnumerable<Func<IEnumerable<T>, object>> aggregations,
    XlsStyleOptions<T>? styles = null)
    : base(title, descriptors.AsEnumerable(), styles)
  {
    GroupDefs = descriptors.Where(p => p is GroupingColumnDescriptor<T>)
      .Cast<GroupingColumnDescriptor<T>>();

    PropDefs = descriptors.Where(p => p is not GroupingColumnDescriptor<T>);

    Aggregations = aggregations;
  }

  /// <remarks>
  /// The roles this reporter adds to the flat one. A resolver has already had its say by the time
  /// this runs, so an override here only decides the cells it declined.
  /// </remarks>
  protected override ICellStyle DefaultStyleFor(IWorkbook workbook, XlsCellContext<T> context) => context.Role switch
  {
    XlsCellRole.Data when context.Item is not null && context.Column is not null =>
      GetDataStyle(workbook, context.Item, context.RowIndex, context.Column),
    XlsCellRole.Aggregation => GetAggregationStyle(workbook),
    XlsCellRole.Region => GetRegionStyle(workbook),
    _ => base.DefaultStyleFor(workbook, context)
  };

  /// <summary>The style of a value cell inside a group.</summary>
  /// <remarks>
  /// <paramref name="index"/> is the row on the sheet. It was the row's position within its group
  /// before styling moved through <see cref="XlsCellContext{T}"/>, which counts from the top.
  /// </remarks>
  protected virtual ICellStyle GetDataStyle(IWorkbook workbook, T item, int index, ColumnDescriptor<T> column) =>
    ThemeStyle(workbook, XlsCellRole.Data);

  protected virtual ICellStyle GetAggregationStyle(IWorkbook workbook) =>
    ThemeStyle(workbook, XlsCellRole.Aggregation);

  protected virtual ICellStyle GetRegionStyle(IWorkbook workbook) =>
    ThemeStyle(workbook, XlsCellRole.Region);

  protected virtual void ApplyRegionStyle(ISheet sheet, CellRangeAddress range)
  {
    sheet.RegionWithBorderStyle(range, BorderStyle.Thin);
  }

  private void CreateGroupSection(IWorkbook workbook, ISheet sheet, GroupingItem<T> node, int baseRowIndex, int baseColIndex)
  {
    var verticalRange = new CellRangeAddress(
        baseRowIndex,
        baseRowIndex + node.Items.Count() + node.AllSubNodes.Count() * (Aggregations.Any() ? 2 : 1) + (Aggregations.Any() ? 1 : 0),
        baseColIndex,
        baseColIndex);

    var horizontalRange = new CellRangeAddress(baseRowIndex, baseRowIndex, baseColIndex + 1, baseColIndex + 1 + node.Depth - 1 + PropDefs.Count() - 1);

    sheet.GetRow(verticalRange.FirstRow).GetCell(verticalRange.FirstColumn).SetCellValue("-" ?? "")
      .WithStyle(StyleFor(workbook, new XlsCellContext<T>(
        XlsCellRole.Region, null, verticalRange.FirstColumn, verticalRange.FirstRow, default, node.Value)));
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
        foreach (var prop in PropDefs)
        {
          var rowIndex = baseRowIndex + 1 + row;
          var colIndex = baseColIndex + 1 + col++;
          var value = prop.SelectValue(item);

          var cell = sheet.GetRow(rowIndex).GetCell(colIndex).SetCellValue(value?.ToString() ?? "");
          cell.CellStyle = StyleFor(workbook,
            new XlsCellContext<T>(XlsCellRole.Data, prop, colIndex, rowIndex, item, value));
        }
        row++;
      }
    }

    var lastRow = node.Items.Count() + node.AllSubNodes.Count() * 2;
    var firstCol = node.Depth - 1;
    foreach (var agg in Aggregations)
    {
      var rowIndex = baseRowIndex + 1 + lastRow;
      var colIndex = baseColIndex + 1 + firstCol++;
      var value = agg.Invoke(node.Items);

      var cell = sheet.GetRow(rowIndex).GetCell(colIndex).SetCellValue(value?.ToString() ?? "");
      cell.CellStyle = StyleFor(workbook,
        new XlsCellContext<T>(XlsCellRole.Aggregation, null, colIndex, rowIndex, default, value));
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
            children[j].AllSubNodes.Count() * (Aggregations.Any() ? 2 : 1) +
            (Aggregations.Any() ? 2 : 1)).Sum(),
        baseColIndex);
    }
  }


  public override async Task WriteReportAsync(IEnumerable<T> data, Stream output)
  {
    ResetStyles();

    XSSFWorkbook workbook = new();

    ISheet sheet = workbook.CreateSheet("Report");

    var headerRow = sheet.CreateRow(0);
    headerRow.HeightInPoints = 40;

    var headerCell = 0;
    foreach (var column in GroupDefs.Cast<ColumnDescriptor<T>>().Concat(PropDefs))
    {
      var colIndex = headerCell++;
      var cell = headerRow.CreateCell(colIndex).SetCellValue(column.Label);
      cell.CellStyle = StyleFor(workbook,
        new XlsCellContext<T>(XlsCellRole.Header, column, colIndex, 0, default, column.Label));
    }

    int rowOffset = 1;
    int colOffset = 0;
    int groupColumns = GroupDefs.Count();
    int propertiesColumns = PropDefs.Count();
    int totalColumns = groupColumns + propertiesColumns;

    var groupings = GroupDefs.ExecuteGrouping(data);

    int totalRows = groupings.Select(p =>
      p.Items.Count() +
      p.AllSubNodes.Count() * (Aggregations.Any() ? 2 : 1) +
      (Aggregations.Any() ? 2 : 1)).Sum();

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

    // After auto-sizing: an explicit column width set here is meant to win over it.
    Styles.PostProcess?.Invoke(workbook, sheet);

    workbook.Write(output, true);

    await Task.CompletedTask;
  }
}
