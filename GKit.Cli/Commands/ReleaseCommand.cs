using System.Diagnostics;
using System.IO.Compression;

namespace GKit.Cli.Commands;

/// <summary>
/// The release.sh logic the reference applications each carried a drifting copy of: publish
/// single-file and framework-dependent, then zip into build/ as &lt;Project&gt;.&lt;height&gt;-&lt;branch&gt;.zip.
/// </summary>
public static class ReleaseCommand
{
  public static string ArtifactName(string project, int gitHeight, string branch) =>
    $"{project}.{gitHeight}-{branch}.zip";

  public static int Run(Workspace workspace, IReadOnlyList<string> projects, string rid, string configuration,
      string framework, TextWriter output)
  {
    var targets = projects.Count > 0
      ? projects.ToList()
      : workspace.ProjectPaths.Where(IsPublishable).ToList();

    if (targets.Count == 0)
    {
      output.WriteLine("No publishable project found. Pass one explicitly: gkit release <project>.");
      return 1;
    }

    var branch = Git(workspace.Root, "rev-parse", "--abbrev-ref", "HEAD") ?? "nogit";
    var height = int.TryParse(Git(workspace.Root, "rev-list", "--count", "HEAD"), out var value) ? value : 0;

    var buildDirectory = Path.Combine(workspace.Root, "build");
    Directory.CreateDirectory(buildDirectory);

    foreach (var zip in Directory.EnumerateFiles(buildDirectory, "*.zip")) File.Delete(zip);

    foreach (var target in targets)
    {
      var projectPath = Path.GetFullPath(target);
      var name = Path.GetFileNameWithoutExtension(projectPath);

      output.WriteLine($"Publishing {name} for {rid}");

      var exitCode = Dotnet(workspace.Root,
        "publish", projectPath,
        "-c", configuration,
        "--runtime", rid,
        "-p:PublishSingleFile=true",
        "--no-self-contained");

      if (exitCode != 0)
      {
        output.WriteLine($"publish failed for {name} (exit {exitCode})");
        return exitCode;
      }

      var publishDirectory = Path.Combine(Path.GetDirectoryName(projectPath)!,
        "bin", configuration, framework, rid, "publish");

      if (!Directory.Exists(publishDirectory))
      {
        output.WriteLine($"No publish output at {publishDirectory}");
        return 1;
      }

      var artifact = Path.Combine(buildDirectory, ArtifactName(name, height, branch));
      ZipFile.CreateFromDirectory(publishDirectory, artifact, CompressionLevel.Optimal, includeBaseDirectory: false);

      output.WriteLine($"  {workspace.RelativeTo(artifact)}");
    }

    return 0;
  }

  private static bool IsPublishable(string projectPath)
  {
    var text = File.ReadAllText(projectPath);
    return text.Contains("Microsoft.NET.Sdk.Web") ||
           text.Contains("<OutputType>Exe</OutputType>", StringComparison.OrdinalIgnoreCase);
  }

  private static string? Git(string workingDirectory, params string[] arguments)
  {
    var startInfo = new ProcessStartInfo("git")
    {
      WorkingDirectory = workingDirectory,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };
    foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

    try
    {
      using var process = Process.Start(startInfo);
      if (process is null) return null;

      var output = process.StandardOutput.ReadToEnd().Trim();
      process.WaitForExit();
      return process.ExitCode == 0 ? output : null;
    }
    catch (Exception)
    {
      return null;
    }
  }

  private static int Dotnet(string workingDirectory, params string[] arguments)
  {
    var startInfo = new ProcessStartInfo("dotnet") { WorkingDirectory = workingDirectory };
    foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

    using var process = Process.Start(startInfo);
    if (process is null) return 1;

    process.WaitForExit();
    return process.ExitCode;
  }
}
