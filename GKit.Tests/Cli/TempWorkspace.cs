namespace GKit.Tests.Cli;

/// <summary>
/// A throwaway directory tree standing in for a scaffolded solution. The CLI works on files it
/// cannot evaluate, so the tests give it real files rather than mocks.
/// </summary>
public sealed class TempWorkspace : IDisposable
{
  public string Root { get; }

  public TempWorkspace()
  {
    Root = Path.Combine(Path.GetTempPath(), "gkit-tests", Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(Root);
  }

  public string Write(string relativePath, string content)
  {
    var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, content);
    return path;
  }

  public string Read(string relativePath) =>
    File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

  public string WriteSolution(string name = "Acme.slnx") =>
    Write(name, "<Solution>\n</Solution>\n");

  /// <summary>A minimal web project referencing the given packages as "id:version" pairs.</summary>
  public string WriteProject(string relativePath, params string[] packages)
  {
    var references = string.Join("\n    ",
      packages.Select(p =>
      {
        var parts = p.Split(':');
        return parts.Length > 1
          ? $"""<PackageReference Include="{parts[0]}" Version="{parts[1]}" />"""
          : $"""<PackageReference Include="{parts[0]}" />""";
      }));

    return Write(relativePath, $"""
      <Project Sdk="Microsoft.NET.Sdk.Web">

        <PropertyGroup>
          <TargetFramework>net10.0</TargetFramework>
        </PropertyGroup>

        <ItemGroup>
          {references}
        </ItemGroup>

      </Project>
      """);
  }

  public void Dispose()
  {
    try
    {
      if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }
    catch (IOException)
    {
      // A leftover temp directory is not worth failing a test over.
    }
  }
}
