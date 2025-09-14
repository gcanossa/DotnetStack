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

        var pdfDocument = PdfReader.Open(pdfStream);

        var fonts = new List<XFont>();
        var pages = new Dictionary<int, XTextFormatter>();
        foreach (var spec in GetFields())
        {
            var font = fonts.FirstOrDefault(p => p.FontFamily.Name == spec.FontName && Math.Abs(p.Size - spec.FontSize) < 0.01);
            if (font == null)
                fonts.Add(font = new XFont(spec.FontName, spec.FontSize));
            
            if(!pages.ContainsKey(spec.PageNumber))
                pages.Add(spec.PageNumber, new XTextFormatter(XGraphics.FromPdfPage(pdfDocument.Pages[spec.PageNumber - 1])));
            
            var page =  pages[spec.PageNumber];

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

        await pdfDocument.SaveAsync(outputPdf, false);
    }

    protected abstract IEnumerable<PdfStamperField<T>> GetFields();
}