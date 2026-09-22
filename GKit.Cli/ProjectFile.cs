using System.Xml.Linq;

namespace GKit.Cli;

/// <summary>
/// Minimal, formatting-preserving-ish editor for a csproj. Deliberately XDocument rather than the
/// MSBuild object model: the tool must work on projects it cannot evaluate, such as one whose
/// package references do not resolve yet - which is the whole point of <c>gkit link</c>.
/// </summary>
public class ProjectFile
{
  public string Path { get; }
  public XDocument Document { get; }

  private readonly bool _hadDeclaration;
  private readonly bool _hadBom;

  private ProjectFile(string path, XDocument document, bool hadDeclaration, bool hadBom)
  {
    Path = path;
    Document = document;
    _hadDeclaration = hadDeclaration;
    _hadBom = hadBom;
  }

  public static ProjectFile Load(string path)
  {
    var text = File.ReadAllText(path);
    return ParseText(path, text, HasBom(path));
  }

  public static ProjectFile ParseText(string path, string xml) => ParseText(path, xml, false);

  private static ProjectFile ParseText(string path, string xml, bool hadBom)
  {
    var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
    return new ProjectFile(path, document, xml.TrimStart('﻿').StartsWith("<?xml"), hadBom);
  }

  private static bool HasBom(string path)
  {
    using var stream = File.OpenRead(path);
    Span<byte> head = stackalloc byte[3];
    return stream.Read(head) == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
  }

  /// <summary>
  /// Writes the document back without reformatting it, and without inventing an XML declaration
  /// the file did not have: these projects are read by humans in diffs.
  /// </summary>
  public void Save() => XmlIo.Write(Path, Document, _hadDeclaration, _hadBom);

  public IEnumerable<XElement> PackageReferenceElements =>
    Document.Descendants("PackageReference").Where(p => p.Attribute("Include") is not null);

  public IEnumerable<PackageRef> PackageReferences =>
    PackageReferenceElements.Select(p => new PackageRef(
      p.Attribute("Include")!.Value,
      p.Attribute("Version")?.Value ?? p.Element("Version")?.Value,
      p));

  public IEnumerable<XElement> ProjectReferenceElements =>
    Document.Descendants("ProjectReference").Where(p => p.Attribute("Include") is not null);

  public IEnumerable<string> ProjectReferences =>
    ProjectReferenceElements.Select(p => p.Attribute("Include")!.Value);

  /// <summary>
  /// Raw &lt;Reference&gt; elements with a HintPath. These bind to a built dll on one developer's
  /// machine and are always a defect in this codebase - Benati.Rifiuti.Data carried one into
  /// GKit.RENTRI/bin/Debug.
  /// </summary>
  public IEnumerable<(string Include, string HintPath)> HintPathReferences =>
    Document.Descendants("Reference")
      .Where(p => p.Element("HintPath") is not null)
      .Select(p => (p.Attribute("Include")?.Value ?? "", p.Element("HintPath")!.Value));

  /// <summary>
  /// Replaces a PackageReference with a ProjectReference, in place, so surrounding items keep
  /// their order. Returns the version that was replaced, for the sidecar file unlink reads.
  /// </summary>
  public string? SwapPackageForProject(string packageId, string relativeProjectPath)
  {
    var element = PackageReferenceElements
      .FirstOrDefault(p => string.Equals(p.Attribute("Include")!.Value, packageId, StringComparison.OrdinalIgnoreCase));

    if (element is null) return null;

    var version = element.Attribute("Version")?.Value ?? element.Element("Version")?.Value;

    var replacement = new XElement("ProjectReference", new XAttribute("Include", relativeProjectPath));
    element.ReplaceWith(replacement);

    return version ?? "";
  }

  /// <summary>Inverse of <see cref="SwapPackageForProject"/>.</summary>
  public bool SwapProjectForPackage(string relativeProjectPath, string packageId, string? version)
  {
    var element = ProjectReferenceElements.FirstOrDefault(p =>
      NormalizePath(p.Attribute("Include")!.Value) == NormalizePath(relativeProjectPath));

    if (element is null) return false;

    var replacement = new XElement("PackageReference", new XAttribute("Include", packageId));
    if (!string.IsNullOrEmpty(version)) replacement.Add(new XAttribute("Version", version));

    element.ReplaceWith(replacement);
    return true;
  }

  public bool RenamePackage(string fromId, string toId)
  {
    var element = PackageReferenceElements
      .FirstOrDefault(p => string.Equals(p.Attribute("Include")!.Value, fromId, StringComparison.OrdinalIgnoreCase));

    if (element is null) return false;

    element.Attribute("Include")!.Value = toId;
    return true;
  }

  public bool SetPackageVersion(string packageId, string version)
  {
    var element = PackageReferenceElements
      .FirstOrDefault(p => string.Equals(p.Attribute("Include")!.Value, packageId, StringComparison.OrdinalIgnoreCase));

    if (element is null) return false;

    if (element.Attribute("Version") is { } attribute) attribute.Value = version;
    else if (element.Element("Version") is { } child) child.Value = version;
    else element.Add(new XAttribute("Version", version));

    return true;
  }

  public static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('.', '/');

  public record PackageRef(string Id, string? Version, XElement Element);
}
