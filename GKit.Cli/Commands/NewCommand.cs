using System.Diagnostics;

namespace GKit.Cli.Commands;

/// <summary>
/// Thin wrapper over 'dotnet new gkit-*'. The templates stay the single source of truth; this only
/// normalises the ergonomics, chiefly the comma separated --features list the proposal documents
/// (dotnet new wants one flag per value).
/// </summary>
public static class NewCommand
{
  public static readonly string[] Templates = ["sln", "app", "shared", "data", "migrator", "worker", "test"];

  public static IReadOnlyList<string> BuildArguments(string template, string name, IReadOnlyList<string> passThrough)
  {
    var arguments = new List<string> { "new", $"gkit-{template}", "-n", name };

    for (var i = 0; i < passThrough.Count; i++)
    {
      var argument = passThrough[i];

      // --features quartz,reporting -> --features quartz --features reporting
      if (argument is "--features" && i + 1 < passThrough.Count)
      {
        foreach (var value in passThrough[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
          arguments.Add("--features");
          arguments.Add(value);
        }

        i++;
        continue;
      }

      arguments.Add(argument);
    }

    return arguments;
  }

  public static int Run(string template, string name, IReadOnlyList<string> passThrough, string workingDirectory,
      TextWriter output) =>
    Run(template, name, passThrough, workingDirectory, output, FeatureManifest.Load());

  public static int Run(string template, string name, IReadOnlyList<string> passThrough, string workingDirectory,
      TextWriter output, FeatureManifest manifest)
  {
    if (!Templates.Contains(template))
    {
      output.WriteLine($"Unknown template '{template}'. Known: {string.Join(", ", Templates)}.");
      return 1;
    }

    var arguments = BuildArguments(template, name, passThrough);

    var startInfo = new ProcessStartInfo("dotnet") { WorkingDirectory = workingDirectory };
    foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

    var process = Process.Start(startInfo);
    if (process is null)
    {
      output.WriteLine("Could not start 'dotnet'.");
      return 1;
    }

    process.WaitForExit();

    if (process.ExitCode == 127 || process.ExitCode == 103)
    {
      output.WriteLine();
      output.WriteLine("The GKit templates do not seem to be installed. Run:");
      output.WriteLine("  dotnet new install GKit.Templates");
      return process.ExitCode;
    }

    if (process.ExitCode == 0 && template != "sln") AdoptCentralPackages(workingDirectory, name, manifest, output);

    return process.ExitCode;
  }

  /// <summary>
  /// A solution created by gkit-sln manages versions centrally, but the project templates emit
  /// inline versions because a template cannot see its sibling. Reconcile the two here rather than
  /// letting the generated solution fail to restore with NU1008.
  /// </summary>
  internal static void AdoptCentralPackages(string workingDirectory, string name, FeatureManifest manifest,
      TextWriter output)
  {
    var workspace = Workspace.Discover(workingDirectory);
    if (workspace.DirectoryPackagesPropsPath is null) return;

    var central = CentralPackages.Load(workspace.DirectoryPackagesPropsPath);
    if (!central.IsEnabled) return;

    var gkitPackages = manifest.GKitPackages.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var moved = 0;

    foreach (var projectPath in workspace.ProjectPaths)
    {
      if (!Path.GetFileNameWithoutExtension(projectPath).Equals(name, StringComparison.OrdinalIgnoreCase)) continue;

      var project = ProjectFile.Load(projectPath);
      var hoisted = central.Hoist(project, gkitPackages);
      if (hoisted.Count == 0) continue;

      project.Save();
      moved += hoisted.Count;
    }

    if (moved == 0) return;

    central.Save();
    output.WriteLine($"Moved {moved} package version(s) into {workspace.RelativeTo(workspace.DirectoryPackagesPropsPath)}.");
  }
}
