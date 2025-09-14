namespace GKit.Pdf;

public class ReflectionPdfStamper<T> : AbstractPdfStamper<T> where T : class
{
    protected override IEnumerable<PdfStamperField<T>> GetFields()
    {
        var fields = typeof(T).GetProperties()
            .ToDictionary(
                p => p,
                p => p.GetCustomAttributes(true).FirstOrDefault(t => t is PdfStamperFieldAttribute) as PdfStamperFieldAttribute);
        
        return fields.Where(kv => kv.Value is not null)
            .Select(kv => new PdfStamperField<T>(p => kv.Key.GetValue(p), kv.Value!.Left, kv.Value!.Top, kv.Value!.PageNumber)
            {
                Width = kv.Value.Width,
                Height = kv.Value.Height,
                FontName = kv.Value.FontName,
                FontSize = kv.Value.FontSize
            });
    }
}