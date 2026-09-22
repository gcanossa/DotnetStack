namespace GKit.Cli.Commands;

/// <summary>
/// Aligns every GKit.* pin in the solution onto one version, preferring Directory.Packages.props
/// when the solution uses Central Package Management.
/// </summary>
public static class UpdateCommand
{
  public static int Run(Workspace workspace, FeatureManifest manifest, string? requestedVersion, bool dryRun,
      TextWriter output)
  {
    var gkitPackages = manifest.GKitPackages.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var projects = workspace.ProjectPaths.Select(p => (Path: p, Project: ProjectFile.Load(p))).ToList();

    var found = projects
      .SelectMany(p => p.Project.PackageReferences.Select(r => (p.Path, Reference: r)))
      .Where(p => gkitPackages.Contains(p.Reference.Id) && p.Reference.Version is not null)
      .ToList();

    if (found.Count == 0)
    {
      output.WriteLine("No versioned GKit package references found.");
      return 0;
    }

    var target = requestedVersion ?? Highest(found.Select(p => p.Reference.Version!));
    output.WriteLine($"Target version: {target}");

    var changes = 0;

    foreach (var (path, reference) in found)
    {
      if (string.Equals(reference.Version, target, StringComparison.OrdinalIgnoreCase)) continue;

      output.WriteLine($"  {workspace.RelativeTo(path)}: {reference.Id} {reference.Version} -> {target}");
      changes++;

      if (dryRun) continue;

      var project = projects.First(p => p.Path == path).Project;
      project.SetPackageVersion(reference.Id, target);
    }

    if (!dryRun)
    {
      foreach (var path in found.Select(p => p.Path).Distinct())
      {
        projects.First(p => p.Path == path).Project.Save();
      }
    }

    output.WriteLine(changes == 0
      ? "Every GKit reference already matches."
      : dryRun ? $"{changes} reference(s) would change." : $"Updated {changes} reference(s).");

    return 0;
  }

  /// <summary>
  /// Orders NuGet-ish versions numerically so 0.0.9 does not beat 0.0.19, which plain string
  /// comparison gets wrong. Floating ranges ("0.1.*") sort last: they already track the newest.
  /// </summary>
  internal static string Highest(IEnumerable<string> versions) =>
    versions.Distinct().OrderBy(Key).Last();

  private static (int Floating, int Major, int Minor, int Patch, string Raw) Key(string version)
  {
    var floating = version.Contains('*') ? 1 : 0;
    var parts = version.TrimEnd('*', '.').Split('.', StringSplitOptions.RemoveEmptyEntries);

    int Part(int index) => parts.Length > index && int.TryParse(parts[index], out var value) ? value : 0;

    return (floating, Part(0), Part(1), Part(2), version);
  }
}
