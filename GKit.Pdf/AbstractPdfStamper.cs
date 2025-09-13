using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;

namespace GKit.Pdf;

public abstract class AbstractPdfStamper<T> where T : class
{
    public async Task StampWithModelAsync(PdfDocument pdfDocument, T model, Stream outputPdf, IFormatProvider formatProvider)
    {
        _ = pdfDocument ??  throw new ArgumentNullException(nameof(pdfDocument));
        _ = model ??  throw new ArgumentNullException(nameof(model));
        _ = outputPdf ??  throw new ArgumentNullException(nameof(outputPdf));
        _ = formatProvider ??  throw new ArgumentNullException(nameof(formatProvider));

        var fonts = new List<XFont>();
        var pages = new Dictionary<int, XTextFormatter>();
        foreach (var spec in GetFields())
        {
            var font = fonts.FirstOrDefault(p => p.FontFamily.Name == spec.FontName && Math.Abs(p.Size - spec.FontSize) < 0.01);
            if (font == null)
                fonts.Add(font = new XFont(spec.FontName, spec.FontSize));
            
            if(!pages.ContainsKey(spec.Page))
                pages.Add(spec.Page, new XTextFormatter(XGraphics.FromPdfPage(pdfDocument.Pages[spec.Page - 1])));
            
            var page =  pages[spec.Page];

            var propertyValue = spec.SelectValue(model);
            var isNumber = propertyValue is int or long or double or float or decimal;
            var text = propertyValue is null ? string.Empty : string.Format(formatProvider, isNumber ? "{0:N}" : "{0}", propertyValue);

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

        pdfDocument.Save(outputPdf, false);

        await Task.CompletedTask;
    }

    protected abstract IEnumerable<PdfStamperField<T>> GetFields();
}