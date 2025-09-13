using System.Globalization;
using System.Runtime.InteropServices.ComTypes;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace GKit.Pdf;

public static class PdfStamper
{
    public static Task StampWithModel<T>(this PdfDocument pdfDocument, T model, Stream outputPdf)
    {
        return pdfDocument.StampWithModel(model, outputPdf, CultureInfo.InvariantCulture);
    }
    
    public static Task StampWithModel<T>(this PdfDocument pdfDocument, T model, Stream outputPdf, IFormatProvider formatProvider)
    {
        _ = pdfDocument ??  throw new ArgumentNullException(nameof(pdfDocument));
        _ = model ??  throw new ArgumentNullException(nameof(model));

        var fields = typeof(T).GetProperties()
            .ToDictionary(
                p => p,
                p => p.GetCustomAttributes(true).FirstOrDefault(t => t is PdfStamperFieldAttribute) as PdfStamperFieldAttribute);

        var fonts = new List<XFont>();
        var pages = new Dictionary<int, XTextFormatter>();
        foreach (var (field, spec) in fields)
        {
            if(spec == null) continue;
            
            var font = fonts.FirstOrDefault(p => p.FontFamily.Name == spec.FontName && Math.Abs(p.Size - spec.FontSize) < 0.01);
            if (font == null)
                fonts.Add(font = new XFont(spec.FontName, spec.FontSize));
            
            if(!pages.ContainsKey(spec.Page))
                pages.Add(spec.Page, new XTextFormatter(XGraphics.FromPdfPage(pdfDocument.Pages[spec.Page - 1])));
            
            var page =  pages[spec.Page];

            var propertyValue = field.GetValue(model);
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

        return Task.CompletedTask;
    }
}