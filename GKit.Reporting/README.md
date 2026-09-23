# GKit.Reporting

The library provide an extension method to the type _IEnumerable\<T\>_ which produces a CSV string.

```cs
string csv = items.ToCsvString(b =>
    b.AddColumn("Number", p => p.Num)
    .AddColumn("Date", p => p.DateNow.ToString("dd/MM/yyyy HH:mm"))
    .AddColumn("Tit", p => p.Title));

```

## XLSX reports

`XlsReporter<T>` writes a header row and one row per item, each value in its native cell type so
Excel formats numbers and dates in the reader's locale rather than the server's.

```cs
var descriptors = new DescriptorsBuilder<Movement>()
    .Column("Codice", p => p.Code)
    .Column("Quantita", p => p.Quantity, "#,##0.00")   // Excel number format, per column
    .Column("Data", p => p.Date)
    .Build();

await new XlsReporter<Movement>("Movimenti", descriptors).WriteReportAsync(data, stream);
```

### Styling without a subclass

`XlsStyleOptions<T>` is the third constructor argument. It carries a `XlsTheme` for the look of the
whole sheet, an optional per-cell `Resolve` for the exceptions, and a `PostProcess` hook that runs
against the finished sheet.

```cs
var styles = new XlsStyleOptions<Movement>
{
    Theme = XlsTheme.Default with
    {
        FontFamily = "Calibri",
        FontSize = 10,
        HeaderBackground = IndexedColors.Grey25Percent,
    },

    // Runs per cell. Return null to keep the style the theme gives it.
    Resolve = ctx => ctx is { Role: XlsCellRole.Data, Value: decimal and < 0 }
        ? new XlsCellStyle("negative", wb => wb.CreateCellStyle()
            .WithFont(wb.CreateFont().FontStyle("Calibri", 10).Color(IndexedColors.Red))
            .BorderStyle(BorderStyle.Thin))
        : null,

    PostProcess = (workbook, sheet) => sheet.CreateFreezePane(0, 1),
};

await new XlsReporter<Movement>("Movimenti", descriptors, styles).WriteReportAsync(data, stream);
```

Two things the design turns on:

- **An options object holds recipes, never an `ICellStyle`.** A style belongs to the workbook that
  created it, so one built ahead of time cannot be used by a second render. That is what makes an
  options object safe to build once and share — a DI singleton, a static, a `[Parameter]`.
- **A resolved style is named, and the name is its cache key.** Returning the same key for a
  thousand cells creates one style, not a thousand: xlsx caps a workbook at roughly 64k cell styles,
  which a per-cell style reaches on an export of any size.

The `Get*Style` methods stay `protected virtual` for a reporter that is a type of its own — 
`XlsGroupingReporter<T>` is one — and a resolver takes precedence over them where it returns
something. Grid exports reach all of this through `GKit.UI.Data`'s `ToXlsAsync` and the grids'
`ExportStyles` parameter.
