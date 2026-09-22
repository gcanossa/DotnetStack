namespace GKit.Cli;

/// <summary>
/// A local checkout of the GKit suite, mapping a published package id onto the csproj that
/// produces it. <c>gkit link</c> needs nothing more than that mapping.
/// </summary>
public class GKitRepository
{
  public string Root { get; }
  private readonly Dictionary<string, string> _projectsByPackageId;

  private GKitRepository(string root, Dictionary<string, string> projects)
  {
    Root = root;
    _projectsByPackageId = projects;
  }

  public IReadOnlyDictionary<string, string> ProjectsByPackageId => _projectsByPackageId;

  /// <summary>
  /// Opens a checkout. A directory qualifies when it holds at least one GKit.&lt;name&gt;/GKit.&lt;name&gt;.csproj,
  /// which is the layout of the DotnetStack repository.
  /// </summary>
  public static GKitRepository? TryOpen(string path)
  {
    var full = Path.GetFullPath(path);
    if (!Directory.Exists(full)) return null;

    var projects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var directory in Directory.EnumerateDirectories(full, "GKit.*"))
    {
      var name = Path.GetFileName(directory);
      var csproj = Path.Combine(directory, name + ".csproj");
      if (File.Exists(csproj)) projects[name] = csproj;
    }

    return projects.Count > 0 ? new GKitRepository(full, projects) : null;
  }

  /// <summary>
  /// Looks for a checkout next to the current one, then beside it, matching the layout the
  /// reference applications assume (../../Personal/DotnetStack from an application repository).
  /// </summary>
  public static GKitRepository? AutoDetect(string startDirectory)
  {
    var candidates = new List<string>();
    var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));

    while (directory is not null)
    {
      candidates.Add(Path.Combine(directory.FullName, "DotnetStack"));
      foreach (var sibling in SafeEnumerateDirectories(directory.FullName))
      {
        candidates.Add(sibling);
        candidates.Add(Path.Combine(sibling, "DotnetStack"));
      }

      directory = directory.Parent;
    }

    return candidates.Distinct().Select(TryOpen).FirstOrDefault(p => p is not null);
  }

  private static IEnumerable<string> SafeEnumerateDirectories(string path)
  {
    try
    {
      return Directory.EnumerateDirectories(path);
    }
    catch (UnauthorizedAccessException)
    {
      return [];
    }
  }
}
