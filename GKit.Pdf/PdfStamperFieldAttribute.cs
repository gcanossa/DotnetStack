using System.Globalization;

namespace GKit.Pdf;

[AttributeUsage(AttributeTargets.Property)]
public class PdfStamperFieldAttribute(double left, double top, int pageNumber) : Attribute
{
    public double Left { get; init; } = left;
    public double Top { get; init; } = top;
    public double Width { get; init; } = Double.PositiveInfinity;
    public double Height { get; init; } = Double.PositiveInfinity;

    public int PageNumber { get; init; } = pageNumber;

    public string FontName { get; init; } = "Arial";
    public double FontSize { get; init; } = 10;
}