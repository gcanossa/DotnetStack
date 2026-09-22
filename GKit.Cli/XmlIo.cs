using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace GKit.Cli;

/// <summary>
/// Round-trips MSBuild XML without reformatting it. The tool edits files a developer reads in a
/// diff, so a one-line change must show up as a one-line change.
/// </summary>
public static class XmlIo
{
  public static void Write(string path, XDocument document, bool includeDeclaration, bool includeBom)
  {
    var settings = new XmlWriterSettings
    {
      OmitXmlDeclaration = !includeDeclaration,
      Indent = false,
      NewLineHandling = NewLineHandling.None,
      Encoding = new UTF8Encoding(includeBom)
    };

    using var stream = File.Create(path);
    using var writer = XmlWriter.Create(stream, settings);
    document.Save(writer);
  }

  /// <summary>
  /// Appends a child with the whitespace the surrounding elements use, so generated entries line
  /// up with hand written ones instead of collapsing onto a single line.
  /// </summary>
  public static void AppendIndented(XElement parent, XElement child)
  {
    var indent = DetectIndent(parent);

    // Drop a trailing whitespace node so the new child lands before the closing tag's indentation.
    if (parent.LastNode is XText trailing && string.IsNullOrWhiteSpace(trailing.Value)) trailing.Remove();

    parent.Add(new XText(Environment.NewLine + indent));
    parent.Add(child);
    parent.Add(new XText(Environment.NewLine + ParentIndent(indent)));
  }

  private static string DetectIndent(XElement parent)
  {
    var existing = parent.Elements().FirstOrDefault();

    if (existing?.PreviousNode is XText text)
    {
      var value = text.Value.Replace("\r", "").Split('\n').LastOrDefault();
      if (!string.IsNullOrEmpty(value) && string.IsNullOrWhiteSpace(value)) return value;
    }

    // Fall back to the parent's own indentation plus two spaces.
    return ParentOwnIndent(parent) + "  ";
  }

  private static string ParentOwnIndent(XElement parent)
  {
    if (parent.PreviousNode is not XText text) return "  ";

    var value = text.Value.Replace("\r", "").Split('\n').LastOrDefault();
    return string.IsNullOrEmpty(value) || !string.IsNullOrWhiteSpace(value) ? value ?? "  " : "  ";
  }

  private static string ParentIndent(string childIndent) =>
    childIndent.Length >= 2 ? childIndent[..^2] : "";
}
