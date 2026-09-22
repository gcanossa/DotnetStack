using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace GKit.Pdf;

public abstract class AbstractPdfStamper<T> where T : class
{
    public async Task StampWithModelAsync(Stream pdfStream, T model, Stream outputPdf, IFormatProvider formatProvider)
    {
        _ = pdfStream ??  throw new ArgumentNullException(nameof(pdfStream));
        _ = model ??  throw new ArgumentNullException(nameof(model));
        _ = outputPdf ??  throw new ArgumentNullException(nameof(outputPdf));
        _ = formatProvider ??  throw new ArgumentNullException(nameof(formatProvider));

        using var pdfDocument = PdfReader.Open(pdfStream);

        var fonts = new List<XFont>();
        var pages = new Dictionary<int, XTextFormatter>();
        var graphics = new List<XGraphics>();

        foreach (var spec in GetFields())
        {
            if (spec.PageNumber < 1 || spec.PageNumber > pdfDocument.PageCount)
                throw new ArgumentOutOfRangeException(nameof(spec.PageNumber),
                    $"Field targets page {spec.PageNumber}, but the document has " +
                    $"{pdfDocument.PageCount} page(s).");

            var font = fonts.FirstOrDefault(p => p.FontFamily.Name == spec.FontName && Math.Abs(p.Size - spec.FontSize) < 0.01);
            if (font == null)
                fonts.Add(font = new XFont(spec.FontName, spec.FontSize));
            
            if (!pages.ContainsKey(spec.PageNumber))
            {
                // Tracked so they can be disposed; previously they were dropped on the floor.
                var pageGraphics = XGraphics.FromPdfPage(pdfDocument.Pages[spec.PageNumber - 1]);
                graphics.Add(pageGraphics);
                pages.Add(spec.PageNumber, new XTextFormatter(pageGraphics));
            }

            
            var page =  pages[spec.PageNumber];

            var propertyValue = spec.SelectValue(model);
            // Honour the field's own format. "{0:N}" was applied to every number, which adds
            // group separators and two decimals to values that never wanted them.
            var text = propertyValue is null
                ? string.Empty
                : string.Format(formatProvider, spec.Format, propertyValue);

            page.DrawString(
                text, 
                font, 
                XBrushes.Black, 
                new XRect(spec.Left, spec.Top, spec.Width, spec.Height),
                new XStringFormat()
                {
                    Alignment = XStringAlignment.Near,
                    LineAlignment = XLineAlignment.Near
                });
        }

        await pdfDocument.SaveAsync(outputPdf, false);

        foreach (var pageGraphics in graphics)
            pageGraphics.Dispose();
    }

    protected abstract IEnumerable<PdfStamperField<T>> GetFields();
}