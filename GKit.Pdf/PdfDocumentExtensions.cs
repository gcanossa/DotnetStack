using System.Globalization;
using System.Runtime.InteropServices.ComTypes;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace GKit.Pdf;

public static class PdfDocumentExtensions
{
    public static async Task StampWithModel<T>(this PdfDocument pdfDocument, T model, Stream outputPdf)
        where T : class
    {
        await pdfDocument.StampWithModel(model, outputPdf, CultureInfo.InvariantCulture);
    }
    
    public static async Task StampWithModel<T>(this PdfDocument pdfDocument, T model, Stream outputPdf, IFormatProvider formatProvider)
        where T : class
    {
        var stamper = new ReflectionPdfStamper<T>();

        await stamper.StampWithModelAsync(pdfDocument, model, outputPdf, formatProvider);
    }
}