using System.Text.RegularExpressions;

namespace GKit.Cli.Commands;

public record Diagnostic(string Severity, string Code, string Message, string? File = null)
{
  public override string ToString() =>
    File is null ? $"{Severity,-7} {Code,-18} {Message}" : $"{Severity,-7} {Code,-18} {File}: {Message}";
}

/// <summary>
/// Reports the things that rot in GKit solutions. Every check here was found in at least one of
/// the reference repositories.
/// </summary>
public static partial class DoctorCommand
{
  public static IReadOnlyList<Diagnostic> Analyze(Workspace workspace, FeatureManifest manifest)
  {
    var diagnostics = new List<Diagnostic>();
    var projects = workspace.ProjectPaths.Select(p => (Path: p, Project: ProjectFile.Load(p))).ToList();

    var gkitPackages = manifest.GKitPackages.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var retired = manifest.RetiredPackages;

    // A HintPath into someone's bin/Debug resolves on exactly one machine.
    foreach (var (path, project) in projects)
    {
      foreach (var (include, hintPath) in project.HintPathReferences)
      {
        diagnostics.Add(new Diagnostic("error", "GKIT001",
          $"<Reference Include=\"{include}\"> uses HintPath '{hintPath}'. Use a PackageReference, or 'gkit link' for a local checkout.",
          workspace.RelativeTo(path)));
      }
    }

    // Retired packages still resolve, so nothing fails until someone wonders why a fix did not land.
    foreach (var (path, project) in projects)
    {
      foreach (var reference in project.PackageReferences)
      {
        if (!retired.TryGetValue(reference.Id, out var replacement)) continue;

        diagnostics.Add(new Diagnostic("warning", "GKIT002",
          $"{reference.Id} is retired: use {replacement.Replacement} ({replacement.Reason}). 'gkit migrate ui' does the rename.",
          workspace.RelativeTo(path)));
      }
    }

    // Version drift: the same GKit package pinned differently in two projects of one solution.
    var versionsByPackage = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    foreach (var (_, project) in projects)
    {
      foreach (var reference in project.PackageReferences)
      {
        if (!gkitPackages.Contains(reference.Id) || reference.Version is null) continue;

        if (!versionsByPackage.TryGetValue(reference.Id, out var versions))
          versionsByPackage[reference.Id] = versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        versions.Add(reference.Version);
      }
    }

    foreach (var (package, versions) in versionsByPackage.Where(p => p.Value.Count > 1).OrderBy(p => p.Key))
    {
      diagnostics.Add(new Diagnostic("warning", "GKIT003",
        $"{package} is pinned to {versions.Count} different versions ({string.Join(", ", versions.OrderBy(p => p))}). Run 'gkit update'."));
    }

    // Central Package Management is what stops GKIT003 recurring.
    if (workspace.DirectoryPackagesPropsPath is null &&
        projects.Count(p => p.Project.PackageReferences.Any(r => gkitPackages.Contains(r.Id))) > 1)
    {
      diagnostics.Add(new Diagnostic("info", "GKIT004",
        "No Directory.Packages.props: more than one project pins GKit packages independently."));
    }

    // Nerdbank.GitVersioning drives release.sh's artefact names.
    if (!File.Exists(Path.Combine(workspace.Root, "version.json")) && workspace.SolutionPath is not null)
    {
      diagnostics.Add(new Diagnostic("info", "GKIT005",
        "No version.json at the solution root: Nerdbank.GitVersioning falls back to 0.0.x."));
    }

    // The toggling pattern the link command replaces.
    foreach (var (path, _) in projects)
    {
      var text = File.ReadAllText(path);
      if (CommentedProjectReference().IsMatch(text))
      {
        diagnostics.Add(new Diagnostic("info", "GKIT006",
          "Commented-out ProjectReference block: 'gkit link' / 'gkit unlink' does this without editing the file by hand.",
          workspace.RelativeTo(path)));
      }
    }

    // A split data layer whose provider project has no Manifest type gives MigrationsAssembly
    // nothing stable to point at.
    foreach (var (path, _) in projects)
    {
      var name = Path.GetFileNameWithoutExtension(path);
      if (!Regex.IsMatch(name, @"\.Data\.EF\.[A-Za-z0-9]+$")) continue;

      var directory = Path.GetDirectoryName(path)!;
      if (!File.Exists(Path.Combine(directory, "Manifest.cs")))
      {
        diagnostics.Add(new Diagnostic("warning", "GKIT007",
          "Provider project has no Manifest.cs for MigrationsAssembly(typeof(Manifest).Assembly.FullName).",
          workspace.RelativeTo(path)));
      }
    }

    return diagnostics;
  }

  public static int Run(Workspace workspace, FeatureManifest manifest, TextWriter output)
  {
    var diagnostics = Analyze(workspace, manifest);

    if (diagnostics.Count == 0)
    {
      output.WriteLine($"No problems found in {workspace.Root}.");
      return 0;
    }

    foreach (var diagnostic in diagnostics) output.WriteLine(diagnostic.ToString());

    var errors = diagnostics.Count(p => p.Severity == "error");
    var warnings = diagnostics.Count(p => p.Severity == "warning");
    output.WriteLine();
    output.WriteLine($"{errors} error(s), {warnings} warning(s), {diagnostics.Count - errors - warnings} note(s).");

    return errors > 0 ? 1 : 0;
  }

  [GeneratedRegex(@"<!--\s*<ProjectReference", RegexOptions.IgnoreCase)]
  private static partial Regex CommentedProjectReference();
}
