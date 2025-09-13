using System.Globalization;
using GKit.Pdf;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Test.Repo.Pdf;

public class PdfStamperTest
{
    [Fact]
    public async Task Test1()
    {
        GKitPdfExtensions.AddGKitBlazorServices(null);
        
        var input = File.Open("../../../Pdf/test/MNLRM.000351.CP.pdf", FileMode.Open, FileAccess.Read);
        var output = File.Open("../../../Pdf/test/Result.pdf", FileMode.OpenOrCreate, FileAccess.Write);

        var model = new TestModel()
        {
            ProducerName = "Pippo Pippi SRL",
            Notes = "Tante belle cose da dire e fare e altro e altro ancora ecc ecc e infine mi taccio.",
            EmissionDate = DateOnly.FromDateTime(DateTime.Now),
            Amount = 1234.5
        };

        var cultureInfo = new CultureInfo("it-IT");
        cultureInfo.NumberFormat.NumberDecimalDigits = 2;
        await PdfReader.Open(input).StampWithModel(model, output, cultureInfo);
    }

    class TestModel
    {
        [PdfStamperField(104, 74, 1)]
        public string ProducerName { get; set; }
        
        [PdfStamperField(32, 669, 1, Width = 200)]
        public string Notes { get; set; }
        
        [PdfStamperField(360, 37, 1)]
        public DateOnly EmissionDate { get; set; }
        
        [PdfStamperField(104, 389, 1)]
        public double Amount { get; set; }
    }
}