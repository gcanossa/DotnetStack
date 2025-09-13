namespace GKit.Pdf;

public class PdfStamperField<T>(Func<T, object?> valueSelector, double left, double top, int page) where T : class
{
    public Func<T, object?> SelectValue { get; init; } = valueSelector;
    public double Left { get; init; } = left;
    public double Top { get; init; } = top;
    public double Width { get; init; } = double.PositiveInfinity;
    public double Height { get; init; } = double.PositiveInfinity;

    public int Page { get; init; } = page;

    public string FontName { get; init; } = "Arial";
    public double FontSize { get; init; } = 10;
}