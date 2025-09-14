using System.Globalization;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;

namespace GKit.Pdf;

public static class PdfDocumentExtensions
{
    public static async Task StampWithModel<T>(this Stream pdfStream, T model, Stream outputPdf)
        where T : class
    {
        await pdfStream.StampWithModel(model, outputPdf, CultureInfo.InvariantCulture);
    }
    
    public static async Task StampWithModel<T>(this Stream pdfStream, T model, Stream outputPdf, IFormatProvider formatProvider)
        where T : class
    {
        var stamper = new ReflectionPdfStamper<T>();
        
        await stamper.StampWithModelAsync(pdfStream, model, outputPdf, formatProvider);
    }

    public static async ValueTask<string> ReadAllText(this Stream pdfStream, int pageNumber)
    {
        var pdfDocument = PdfReader.Open(pdfStream);
        
        var sb = new StringBuilder();
        var page = pdfDocument.Pages[pageNumber - 1];
        var content = ContentReader.ReadContent(page);

        foreach (var text in content.SelectMany(c => c.ExtractText()))
        {
            sb.Append(text);
        }
        
        return await ValueTask.FromResult(sb.ToString());
    }

    private static IEnumerable<string> ExtractText(this CObject cObject)
    {
        switch (cObject)
        {
            case COperator { OpCode.Name: not (nameof(OpCodeName.Tj) or nameof(OpCodeName.TJ)) }:
                yield break;
            case COperator cOperator:
            {
                foreach (var txt in cOperator.Operands.SelectMany(ExtractText))
                    yield return txt;
                break;
            }
            case CSequence cSequence:
            {
                foreach (var txt in cSequence.SelectMany(ExtractText))
                    yield return txt;
                break;
            }
            case CString cString:
                yield return cString.Value;
                break;
        }
    }
}