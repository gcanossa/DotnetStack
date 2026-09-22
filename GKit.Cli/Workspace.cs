namespace GKit.Cli;

/// <summary>
/// The solution the tool is operating on: located by walking up from the working directory to the
/// first directory holding a .slnx or .sln, falling back to the working directory itself.
/// </summary>
public class Workspace
{
  public string Root { get; }
  public string? SolutionPath { get; }

  private Workspace(string root, string? solutionPath)
  {
    Root = root;
    SolutionPath = solutionPath;
  }

  public static Workspace Discover(string startDirectory)
  {
    var directory = new DirectoryInfo(System.IO.Path.GetFullPath(startDirectory));

    while (directory is not null)
    {
      var solution = directory.EnumerateFiles("*.slnx").Concat(directory.EnumerateFiles("*.sln"))
        .OrderBy(p => p.Extension) // .sln sorts before .slnx, prefer .slnx by taking Last
        .LastOrDefault();

      if (solution is not null) return new Workspace(directory.FullName, solution.FullName);

      directory = directory.Parent;
    }

    var fallback = System.IO.Path.GetFullPath(startDirectory);
    return new Workspace(fallback, null);
  }

  public IEnumerable<string> ProjectPaths =>
    Directory.EnumerateFiles(Root, "*.csproj", SearchOption.AllDirectories)
      .Where(p => !IsBuildOutput(p))
      .OrderBy(p => p, StringComparer.Ordinal);

  public IEnumerable<ProjectFile> Projects => ProjectPaths.Select(ProjectFile.Load);

  public string? DirectoryPackagesPropsPath
  {
    get
    {
      var path = System.IO.Path.Combine(Root, "Directory.Packages.props");
      return File.Exists(path) ? path : null;
    }
  }

  public static bool IsBuildOutput(string path)
  {
    var normalized = path.Replace('\\', '/');
    return normalized.Contains("/bin/") || normalized.Contains("/obj/");
  }

  public string RelativeTo(string absolutePath) =>
    System.IO.Path.GetRelativePath(Root, absolutePath).Replace('\\', '/');
}
