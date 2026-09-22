using System.Globalization;

namespace GKit.Reporting;

/// <summary>
/// Formats report cell values without taking a dependency on the ambient culture.
/// <para>
/// A report generated on an it-IT server is read by machines and people elsewhere; emitting
/// "1,5" for a decimal makes the CSV unparseable (the separator is a comma) and makes the XLSX
/// non-numeric. Report content must not change with the server's locale.
/// </para>
/// </summary>
internal static class InvariantValue
{
  public static string Format(object? value) => value switch
  {
    null => "",
    string s => s,
    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
    _ => value.ToString() ?? ""
  };
}
