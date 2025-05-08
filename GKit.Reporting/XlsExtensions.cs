namespace GKit.Reporting;

using NPOI.SS.Formula.Functions;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

public static class XlsExtensions
{
  public static ICellStyle CloneStyle(this IWorkbook workbook, ICellStyle style)
  {
    var newStyle = workbook.CreateCellStyle();
    newStyle.CloneStyleFrom(style);

    return newStyle;
  }
  public static ICellStyle BorderStyle(this ICellStyle style, BorderStyle borderStyle)
  {
    style.BorderRight = borderStyle;
    style.BorderBottom = borderStyle;
    style.BorderLeft = borderStyle;
    style.BorderTop = borderStyle;

    return style;
  }
  public static ICellStyle BorderColor(this ICellStyle style, IndexedColors borderColor)
  {
    style.RightBorderColor = borderColor.Index;
    style.BottomBorderColor = borderColor.Index;
    style.LeftBorderColor = borderColor.Index;
    style.TopBorderColor = borderColor.Index;

    return style;
  }
  public static ICellStyle WithBackground(this ICellStyle style, IndexedColors color)
  {
    style.FillForegroundColor = color.Index;
    style.FillPattern = FillPattern.SolidForeground;

    return style;
  }
  public static ICellStyle WithFont(this ICellStyle style, IFont font)
  {
    style.SetFont(font);

    return style;
  }

  public static ICellStyle VerticalAlign(this ICellStyle style, VerticalAlignment alignment)
  {
    style.VerticalAlignment = alignment;

    return style;
  }

  public static IFont FontStyle(this IFont font, string fontFamily, int fontSize)
  {
    font.FontName = fontFamily;
    font.FontHeightInPoints = fontSize;

    return font;
  }

  public static IFont Bold(this IFont font, bool value = true)
  {
    font.IsBold = value;

    return font;
  }
  public static IFont Italic(this IFont font, bool value = true)
  {
    font.IsItalic = value;

    return font;
  }
  public static IFont StrikeOut(this IFont font, bool value = true)
  {
    font.IsStrikeout = value;

    return font;
  }
  public static ICell WithStyle(this ICell cell, ICellStyle style)
  {
    cell.CellStyle = style;

    return cell;
  }

  public static CellRangeAddress RegionWithBorderStyle(this ISheet sheet, CellRangeAddress range, BorderStyle borderStyle)
  {
    RegionUtil.SetBorderRight(borderStyle, range, sheet);
    RegionUtil.SetBorderBottom(borderStyle, range, sheet);
    RegionUtil.SetBorderLeft(borderStyle, range, sheet);
    RegionUtil.SetBorderTop(borderStyle, range, sheet);

    return range;
  }
}
