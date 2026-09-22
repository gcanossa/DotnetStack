using System.Reflection;
using System.Text.Json;

namespace GKit.Cli;

/// <summary>
/// Reads the manifest embedded from GKit.Templates/features.json.
/// <para>
/// Every piece of knowledge about UI adapters, capability packs, database providers and
/// authentication modes lives there rather than in a command, so adding one is a data row.
/// </para>
/// </summary>
public class FeatureManifest
{
  private readonly JsonElement _root;

  private FeatureManifest(JsonElement root) => _root = root;

  public static FeatureManifest Load()
  {
    using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GKit.Cli.features.json")
                       ?? throw new InvalidOperationException("features.json is not embedded in GKit.Cli");

    return Parse(stream);
  }

  public static FeatureManifest Parse(Stream stream)
  {
    using var document = JsonDocument.Parse(stream,
      new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

    return new FeatureManifest(document.RootElement.Clone());
  }

  public static FeatureManifest ParseText(string json)
  {
    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
    return Parse(stream);
  }

  /// <summary>Every package id the suite publishes, used to decide what link/update/doctor act on.</summary>
  public IReadOnlyList<string> GKitPackages =>
    _root.GetProperty("gkitPackages").EnumerateArray().Select(p => p.GetString()!).ToList();

  public IReadOnlyList<string> FeatureNames =>
    _root.GetProperty("features").EnumerateObject().Select(p => p.Name).ToList();

  public IReadOnlyList<string> UiNames =>
    _root.GetProperty("ui").EnumerateObject().Select(p => p.Name).ToList();

  public Feature? GetFeature(string name) =>
    _root.GetProperty("features").TryGetProperty(name, out var element) ? new Feature(name, element) : null;

  /// <summary>Packages that still resolve but should not be used, with their replacement.</summary>
  public IReadOnlyDictionary<string, RetiredPackage> RetiredPackages =>
    _root.GetProperty("retiredPackages").EnumerateObject()
      .ToDictionary(p => p.Name,
        p => new RetiredPackage(
          p.Value.GetProperty("replacement").GetString()!,
          p.Value.GetProperty("reason").GetString()!));

  public IReadOnlyList<UiRename> UiRenames =>
    _root.GetProperty("uiMigration").GetProperty("renames").EnumerateArray()
      .Select(p => new UiRename(
        p.GetProperty("from").GetString()!,
        p.GetProperty("to").GetString()!,
        p.GetProperty("kind").GetString()!))
      .ToList();

  public IReadOnlyList<UiReport> UiReports =>
    _root.GetProperty("uiMigration").GetProperty("reportOnly").EnumerateArray()
      .Select(p => new UiReport(
        p.GetProperty("pattern").GetString()!,
        p.GetProperty("message").GetString()!))
      .ToList();

  public class Feature(string name, JsonElement element)
  {
    public string Name { get; } = name;

    public string DisplayName => element.GetProperty("displayName").GetString()!;

    public IReadOnlyList<string> Packages =>
      element.GetProperty("packages").EnumerateArray().Select(p => p.GetProperty("id").GetString()!).ToList();

    public string? Using => element.GetProperty("using").GetString();

    public IReadOnlyList<string> Services =>
      element.GetProperty("services").EnumerateArray().Select(p => p.GetString()!).ToList();

    public string? HealthCheck =>
      element.TryGetProperty("healthCheck", out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;

    public IReadOnlyList<string> AppCalls =>
      element.TryGetProperty("appCalls", out var v)
        ? v.EnumerateArray().Select(p => p.GetString()!).ToList()
        : [];

    public string? ConfigurationSection =>
      element.TryGetProperty("configurationSection", out var v) && v.ValueKind != JsonValueKind.Null
        ? v.GetString()
        : null;
  }

  public record RetiredPackage(string Replacement, string Reason);

  public record UiRename(string From, string To, string Kind);

  public record UiReport(string Pattern, string Message);
}
