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

    /// <summary>
    /// Composite format string for the value, e.g. <c>"{0:N2}"</c> or <c>"{0:dd/MM/yyyy}"</c>.
    /// <para>
    /// Defaults to plain <c>"{0}"</c>. Numbers used to be forced through <c>"{0:N}"</c>, so an
    /// integer quantity of 1000 was stamped as "1.000,00" under it-IT with no way to override it.
    /// </para>
    /// </summary>
    public string Format { get; init; } = "{0}";
}