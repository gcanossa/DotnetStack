using System.Xml.Linq;

namespace GKit.Cli;

/// <summary>
/// Moves inline PackageReference versions into Directory.Packages.props.
/// <para>
/// The templates cannot do this themselves: gkit-sln writes the props file and gkit-app writes the
/// project, and neither can see the other. Doing it here also means an existing solution can adopt
/// Central Package Management without hand-editing every csproj - which is what GKIT003 version
/// drift in the reference applications came from.
/// </para>
/// </summary>
public class CentralPackages
{
  public string Path { get; }
  public XDocument Document { get; }

  private CentralPackages(string path, XDocument document)
  {
    Path = path;
    Document = document;
  }

  public static CentralPackages Load(string path) => ParseText(path, File.ReadAllText(path));

  public static CentralPackages ParseText(string path, string xml) =>
    new(path, XDocument.Parse(xml, LoadOptions.PreserveWhitespace));

  public bool IsEnabled =>
    Document.Descendants("ManagePackageVersionsCentrally")
      .Any(p => string.Equals(p.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase));

  public IReadOnlyDictionary<string, string> PackageVersions =>
    Document.Descendants("PackageVersion")
      .Where(p => p.Attribute("Include") is not null)
      .GroupBy(p => p.Attribute("Include")!.Value, StringComparer.OrdinalIgnoreCase)
      .ToDictionary(p => p.Key, p => p.Last().Attribute("Version")?.Value ?? "", StringComparer.OrdinalIgnoreCase);

  public void Save() => XmlIo.Write(Path, Document, includeDeclaration: true, includeBom: true);

  /// <summary>
  /// Records a version, keeping the higher of the two when the package is already listed.
  /// GKit packages go in the "GKit" group so 'gkit update' can find them.
  /// </summary>
  public void SetVersion(string packageId, string version, bool isGKit)
  {
    var existing = Document.Descendants("PackageVersion")
      .FirstOrDefault(p => string.Equals(p.Attribute("Include")?.Value, packageId, StringComparison.OrdinalIgnoreCase));

    if (existing is not null)
    {
      var current = existing.Attribute("Version")?.Value;
      var winner = current is null ? version : Commands.UpdateCommand.Highest([current, version]);
      existing.SetAttributeValue("Version", winner);
      return;
    }

    var group = GroupFor(isGKit ? "GKit" : "Third party");
    XmlIo.AppendIndented(group, new XElement("PackageVersion",
      new XAttribute("Include", packageId),
      new XAttribute("Version", version)));
  }

  private XElement GroupFor(string label)
  {
    var root = Document.Root ?? throw new InvalidOperationException($"{Path} has no root element");

    var group = root.Elements("ItemGroup")
      .FirstOrDefault(p => string.Equals(p.Attribute("Label")?.Value, label, StringComparison.OrdinalIgnoreCase));

    if (group is not null) return group;

    group = new XElement("ItemGroup", new XAttribute("Label", label));
    root.Add(group);
    return group;
  }

  /// <summary>
  /// Strips Version attributes from a project and records them here. Returns the packages moved.
  /// </summary>
  public IReadOnlyList<string> Hoist(ProjectFile project, ISet<string> gkitPackages)
  {
    var moved = new List<string>();

    foreach (var reference in project.PackageReferences.ToList())
    {
      if (reference.Version is null) continue;

      SetVersion(reference.Id, reference.Version, gkitPackages.Contains(reference.Id));

      reference.Element.Attribute("Version")?.Remove();
      reference.Element.Element("Version")?.Remove();

      moved.Add(reference.Id);
    }

    return moved;
  }
}
