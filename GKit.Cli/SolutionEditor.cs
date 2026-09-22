using System.Diagnostics;
using System.Xml.Linq;

namespace GKit.Cli;

/// <summary>
/// Adds and removes projects from the solution. .slnx is edited directly because it is plain XML;
/// the classic .sln format is left to 'dotnet sln', which owns its GUID bookkeeping.
/// </summary>
public class SolutionEditor(string solutionPath)
{
  public string SolutionPath { get; } = solutionPath;

  private bool IsSlnx => SolutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase);

  private string Root => Path.GetDirectoryName(Path.GetFullPath(SolutionPath))!;

  public IReadOnlyList<string> ProjectPaths
  {
    get
    {
      if (!IsSlnx) return [];

      return XDocument.Load(SolutionPath).Descendants("Project")
        .Select(p => p.Attribute("Path")?.Value)
        .Where(p => p is not null)
        .Select(p => p!.Replace('\\', '/'))
        .ToList();
    }
  }

  public bool Add(string projectPath)
  {
    var relative = Path.GetRelativePath(Root, Path.GetFullPath(projectPath)).Replace('\\', '/');

    if (!IsSlnx) return RunDotnetSln("add", projectPath);

    var document = XDocument.Load(SolutionPath, LoadOptions.PreserveWhitespace);
    var solution = document.Root ?? throw new InvalidOperationException($"{SolutionPath} has no root element");

    if (solution.Descendants("Project").Any(p =>
          string.Equals(p.Attribute("Path")?.Value.Replace('\\', '/'), relative, StringComparison.OrdinalIgnoreCase)))
      return false;

    solution.Add(new XElement("Project", new XAttribute("Path", relative)));
    document.Save(SolutionPath);
    return true;
  }

  public bool Remove(string projectPath)
  {
    var relative = Path.GetRelativePath(Root, Path.GetFullPath(projectPath)).Replace('\\', '/');

    if (!IsSlnx) return RunDotnetSln("remove", projectPath);

    var document = XDocument.Load(SolutionPath, LoadOptions.PreserveWhitespace);

    var element = document.Descendants("Project").FirstOrDefault(p =>
      string.Equals(p.Attribute("Path")?.Value.Replace('\\', '/'), relative, StringComparison.OrdinalIgnoreCase));

    if (element is null) return false;

    element.Remove();
    document.Save(SolutionPath);
    return true;
  }

  private bool RunDotnetSln(string verb, string projectPath)
  {
    var process = Process.Start(new ProcessStartInfo("dotnet")
    {
      ArgumentList = { "sln", SolutionPath, verb, projectPath },
      RedirectStandardOutput = true,
      RedirectStandardError = true
    });

    if (process is null) return false;

    process.WaitForExit();
    return process.ExitCode == 0;
  }
}
