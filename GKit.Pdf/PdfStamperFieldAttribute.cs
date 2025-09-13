using System.Globalization;

namespace GKit.Pdf;

[AttributeUsage(AttributeTargets.Property)]
public class PdfStamperFieldAttribute(double left, double top, int page) : Attribute
{
    public double Left { get; init; } = left;
    public double Top { get; init; } = top;
    public double Width { get; init; } = Double.PositiveInfinity;
    public double Height { get; init; } = Double.PositiveInfinity;

    public int Page { get; init; } = page;

    public string FontName { get; init; } = "Arial";
    public double FontSize { get; init; } = 10;
}