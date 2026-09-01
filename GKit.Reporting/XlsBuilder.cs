using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace GKit.Reporting;

public class XlsBuilder(XSSFWorkbook workbook)
{
    protected readonly Dictionary<string, ICellStyle> _stylesCache = [];

    protected virtual ICellStyle MemoCellStyle(string key, Func<ICellStyle> factory)
    {
        if (!_stylesCache.TryGetValue(key, out ICellStyle? value))
        {
            var style = factory.Invoke();
            value = style;
            _stylesCache.Add(key, value);
        }

        return value;
    }

    public XlsBuilder AddStyle(string key, Func<ICellStyle> factory)
    {
        MemoCellStyle(key, factory);
        return this;
    }
    
    public ICellStyle GetStyle(string key)
    {
        return _stylesCache[key];
    }

    public ISheet AddSheet(string title, Action<ISheetBuilder> build)
    {
        var sheet = workbook.CreateSheet(title);

        build.Invoke(new SheetBuilderImpl(sheet));

        return sheet;
    }

    public interface ISheetBuilder
    {
        ISheetBuilder AddRow(Action<IRowBuilder> builder);
    }

    internal class SheetBuilderImpl(ISheet sheet) : ISheetBuilder
    {
        public int RowIndex { get; private set; } = 0;

        public ISheetBuilder AddRow(Action<IRowBuilder> builder)
        {
            builder.Invoke(new RowBuilderImpl(RowIndex, sheet.CreateRow(RowIndex)));
            RowIndex++;

            return this;
        }
    }

    public interface IRowBuilder
    {
        IRowBuilder AddCell(Action<ICell> builder);
    }

    internal class RowBuilderImpl(int rowIndex, IRow row) : IRowBuilder
    {
        public int ColIndex { get; private set; } = 0;

        public IRowBuilder AddCell(Action<ICell> builder)
        {
            var cell = row.CreateCell(ColIndex);
            builder.Invoke(cell);
            ColIndex++;

            return this;
        }
    }
}