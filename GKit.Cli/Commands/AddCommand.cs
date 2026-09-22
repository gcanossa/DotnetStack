using System.Diagnostics;

namespace GKit.Cli.Commands;

/// <summary>
/// Adds a capability or an item to an existing solution.
/// <para>
/// A capability is four edits that must stay in step - package reference, using directive, the
/// Add... call and the health check. Doing three of the four is the usual way a project ends up
/// half wired, so they are applied together from features.json.
/// </para>
/// </summary>
public static class AddCommand
{
  public static readonly string[] Items = ["job", "clirunner", "crud"];

  public static int Run(Workspace workspace, FeatureManifest manifest, string what, IReadOnlyList<string> passThrough,
      string workingDirectory, TextWriter output)
  {
    if (Items.Contains(what)) return AddItem(what, passThrough, workingDirectory, output);

    var feature = manifest.GetFeature(what);
    if (feature is null)
    {
      output.WriteLine($"Unknown feature or item '{what}'.");
      output.WriteLine($"  features: {string.Join(", ", manifest.FeatureNames)}");
      output.WriteLine($"  items:    {string.Join(", ", Items)}");
      return 1;
    }

    return AddFeature(workspace, manifest, feature, output);
  }

  internal static int AddFeature(Workspace workspace, FeatureManifest manifest, FeatureManifest.Feature feature,
      TextWriter output)
  {
    var programPath = workspace.ProjectPaths
      .Select(p => Path.Combine(Path.GetDirectoryName(p)!, "Program.cs"))
      .FirstOrDefault(File.Exists);

    if (programPath is null)
    {
      output.WriteLine("No Program.cs found: run this inside a scaffolded solution.");
      return 1;
    }

    var projectPath = Directory.EnumerateFiles(Path.GetDirectoryName(programPath)!, "*.csproj").First();
    var project = ProjectFile.Load(projectPath);

    // 1. package references
    var central = workspace.DirectoryPackagesPropsPath is { } propsPath ? CentralPackages.Load(propsPath) : null;
    if (central is { IsEnabled: false }) central = null;

    var existing = project.PackageReferences.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var version = ResolveGKitVersion(project, central);
    var added = new List<string>();

    foreach (var package in feature.Packages.Where(p => !existing.Contains(p)))
    {
      var group = project.Document.Descendants("ItemGroup").LastOrDefault() ?? project.Document.Root!;
      var element = new System.Xml.Linq.XElement("PackageReference",
        new System.Xml.Linq.XAttribute("Include", package));

      // Under Central Package Management the version belongs in the props file, and the
      // PackageReference must not carry one - but it still needs a PackageVersion somewhere,
      // or restore fails with NU1010.
      if (central is null && version is not null) element.Add(new System.Xml.Linq.XAttribute("Version", version));

      XmlIo.AppendIndented(group, element);
      added.Add(package);
    }

    if (added.Count > 0)
    {
      project.Save();

      if (central is not null)
      {
        foreach (var package in added) central.SetVersion(package, version ?? "*", isGKit: true);
        central.Save();
      }
    }

    // 2..4 using, service registration, health check, app calls
    var editor = ProgramEditor.Load(programPath);

    if (feature.Using is not null) editor.AddUsing(feature.Using);
    foreach (var statement in feature.Services) editor.AddServiceCall(statement);
    foreach (var statement in feature.AppCalls) editor.AddAppCall(statement);

    var healthWired = feature.HealthCheck is not null && editor.AddHealthCheck(feature.HealthCheck);

    if (editor.Changed) editor.Save(programPath);

    output.WriteLine($"Added {feature.DisplayName}:");
    foreach (var package in added) output.WriteLine($"  package {package}");
    if (feature.Using is not null) output.WriteLine($"  using {feature.Using}");
    foreach (var statement in feature.Services) output.WriteLine($"  {statement}");
    foreach (var statement in feature.AppCalls) output.WriteLine($"  {statement}");

    if (feature.HealthCheck is not null && !healthWired)
      output.WriteLine($"  note: no health check chain found, add 'healthChecks{feature.HealthCheck};' yourself");

    if (feature.ConfigurationSection is not null)
      output.WriteLine($"  configure under \"{feature.ConfigurationSection}\" in appsettings.json");

    if (added.Count == 0 && !editor.Changed) output.WriteLine("  (already wired)");

    if (central is not null && added.Count > 0)
      output.WriteLine($"  version recorded in {workspace.RelativeTo(workspace.DirectoryPackagesPropsPath!)}");

    return 0;
  }

  /// <summary>
  /// Reuses whatever version the solution already pins GKit at - from the central props file when
  /// there is one, otherwise from the project - so a capability added later does not introduce the
  /// drift 'gkit doctor' reports as GKIT003.
  /// </summary>
  private static string? ResolveGKitVersion(ProjectFile project, CentralPackages? central)
  {
    var fromCentral = central?.PackageVersions
      .FirstOrDefault(p => p.Key.StartsWith("GKit.", StringComparison.OrdinalIgnoreCase)).Value;

    if (!string.IsNullOrEmpty(fromCentral)) return fromCentral;

    return project.PackageReferences
      .FirstOrDefault(p => p.Id.StartsWith("GKit.", StringComparison.OrdinalIgnoreCase) && p.Version is not null)
      ?.Version;
  }

  private static int AddItem(string item, IReadOnlyList<string> passThrough, string workingDirectory, TextWriter output)
  {
    var startInfo = new ProcessStartInfo("dotnet") { WorkingDirectory = workingDirectory };
    startInfo.ArgumentList.Add("new");
    startInfo.ArgumentList.Add($"gkit-{item}");
    foreach (var argument in passThrough) startInfo.ArgumentList.Add(argument);

    var process = Process.Start(startInfo);
    if (process is null)
    {
      output.WriteLine("Could not start 'dotnet'.");
      return 1;
    }

    process.WaitForExit();
    return process.ExitCode;
  }
}
