using System.Text.Json;
using System.Text.Json.Serialization;

namespace GKit.Cli;

/// <summary>
/// Sidecar written to .gkit-link.json at the solution root while a checkout is linked.
/// <para>
/// Without it, unlink would have to guess the version each package reference carried. The
/// reference applications solved the same problem by keeping a commented-out block of project
/// references next to the package references and toggling by hand.
/// </para>
/// </summary>
public class LinkState
{
  public const string FileName = ".gkit-link.json";

  [JsonPropertyName("repositoryPath")]
  public string RepositoryPath { get; set; } = "";

  [JsonPropertyName("links")]
  public List<LinkEntry> Links { get; set; } = [];

  private static readonly JsonSerializerOptions Options = new()
  {
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };

  public static string PathFor(string root) => Path.Combine(root, FileName);

  public static LinkState? TryLoad(string root)
  {
    var path = PathFor(root);
    if (!File.Exists(path)) return null;

    return JsonSerializer.Deserialize<LinkState>(File.ReadAllText(path), Options);
  }

  public void Save(string root) =>
    File.WriteAllText(PathFor(root), JsonSerializer.Serialize(this, Options) + Environment.NewLine);

  public static void Delete(string root)
  {
    var path = PathFor(root);
    if (File.Exists(path)) File.Delete(path);
  }

  public class LinkEntry
  {
    [JsonPropertyName("project")] public string Project { get; set; } = "";
    [JsonPropertyName("package")] public string Package { get; set; } = "";
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("reference")] public string Reference { get; set; } = "";
  }
}
