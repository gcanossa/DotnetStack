using GKit.Cli;
using GKit.Cli.Commands;

namespace GKit.Tests.Cli;

public class CentralPackagesTests
{
  private const string Props = """
    <Project>
      <PropertyGroup>
        <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
      </PropertyGroup>

      <ItemGroup Label="GKit">
      </ItemGroup>

      <ItemGroup Label="Third party">
      </ItemGroup>
    </Project>
    """;

  [Fact]
  public void HoistMovesVersionsOutOfTheProject()
  {
    using var workspace = new TempWorkspace();
    var propsPath = workspace.Write("Directory.Packages.props", Props);
    var projectPath = workspace.WriteProject("App/App.csproj", "GKit.Application:0.1.0", "MudBlazor:9.9.0");

    var central = CentralPackages.Load(propsPath);
    var project = ProjectFile.Load(projectPath);

    var moved = central.Hoist(project, new HashSet<string> { "GKit.Application" });

    Assert.Equal(["GKit.Application", "MudBlazor"], moved);
    Assert.All(project.PackageReferences, p => Assert.Null(p.Version));
    Assert.Equal("0.1.0", central.PackageVersions["GKit.Application"]);
    Assert.Equal("9.9.0", central.PackageVersions["MudBlazor"]);
  }

  [Fact]
  public void HoistSortsGKitPackagesIntoTheirOwnGroup()
  {
    using var workspace = new TempWorkspace();
    var propsPath = workspace.Write("Directory.Packages.props", Props);
    var projectPath = workspace.WriteProject("App/App.csproj", "GKit.Quartz:0.1.0", "MudBlazor:9.9.0");

    var central = CentralPackages.Load(propsPath);
    central.Hoist(ProjectFile.Load(projectPath), new HashSet<string> { "GKit.Quartz" });

    var gkitGroup = central.Document.Descendants("ItemGroup")
      .Single(p => p.Attribute("Label")?.Value == "GKit");

    Assert.Equal(["GKit.Quartz"], gkitGroup.Elements("PackageVersion").Select(p => p.Attribute("Include")!.Value));
  }

  [Fact]
  public void AppendedEntriesAreIndentedRatherThanCollapsedOntoOneLine()
  {
    using var workspace = new TempWorkspace();
    var propsPath = workspace.Write("Directory.Packages.props", Props);
    var projectPath = workspace.WriteProject("App/App.csproj", "GKit.Quartz:0.1.0", "GKit.Reporting:0.1.0");

    var central = CentralPackages.Load(propsPath);
    central.Hoist(ProjectFile.Load(projectPath), new HashSet<string> { "GKit.Quartz", "GKit.Reporting" });
    central.Save();

    var lines = workspace.Read("Directory.Packages.props").Split('\n');
    var entries = lines.Where(p => p.Contains("<PackageVersion")).ToList();

    Assert.Equal(2, entries.Count);
    Assert.All(entries, p => Assert.StartsWith("    <PackageVersion", p));
  }

  [Fact]
  public void SetVersionKeepsTheHigherOfTwo()
  {
    var central = CentralPackages.ParseText("Directory.Packages.props", Props);

    central.SetVersion("GKit.Application", "0.0.9", isGKit: true);
    central.SetVersion("GKit.Application", "0.0.19", isGKit: true);

    Assert.Equal("0.0.19", central.PackageVersions["GKit.Application"]);
  }

  [Theory]
  // Plain string ordering puts 0.0.9 above 0.0.19, which is the drift these solutions actually had.
  [InlineData(new[] { "0.0.9", "0.0.19" }, "0.0.19")]
  [InlineData(new[] { "0.0.17", "0.0.17" }, "0.0.17")]
  [InlineData(new[] { "1.2.3", "1.10.0" }, "1.10.0")]
  [InlineData(new[] { "0.1.0", "0.1.*" }, "0.1.*")]
  public void HighestOrdersVersionsNumerically(string[] versions, string expected) =>
    Assert.Equal(expected, UpdateCommand.Highest(versions));
}
