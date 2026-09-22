using GKit.Cli;

namespace GKit.Tests.Cli;

public class ProjectFileTests
{
  private const string Csproj = """
    <Project Sdk="Microsoft.NET.Sdk.Web">

      <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
      </PropertyGroup>

      <ItemGroup>
        <PackageReference Include="GKit.Application" Version="0.0.19" />
        <PackageReference Include="MudBlazor" Version="9.9.0" />
      </ItemGroup>

    </Project>
    """;

  [Fact]
  public void ReadsPackageReferences()
  {
    var project = ProjectFile.ParseText("a.csproj", Csproj);

    Assert.Equal(["GKit.Application", "MudBlazor"], project.PackageReferences.Select(p => p.Id));
    Assert.Equal("0.0.19", project.PackageReferences.First().Version);
  }

  [Fact]
  public void SwapsAPackageForAProjectInPlace()
  {
    var project = ProjectFile.ParseText("a.csproj", Csproj);

    var version = project.SwapPackageForProject("GKit.Application", "../../GKit.Application/GKit.Application.csproj");

    Assert.Equal("0.0.19", version);
    Assert.Equal(["../../GKit.Application/GKit.Application.csproj"], project.ProjectReferences);
    // the untouched reference keeps its position
    Assert.Equal(["MudBlazor"], project.PackageReferences.Select(p => p.Id));
  }

  [Fact]
  public void SwapRoundTripsBackToTheOriginalVersion()
  {
    var project = ProjectFile.ParseText("a.csproj", Csproj);
    const string reference = "../../GKit.Application/GKit.Application.csproj";

    var version = project.SwapPackageForProject("GKit.Application", reference);
    Assert.True(project.SwapProjectForPackage(reference, "GKit.Application", version));

    var restored = project.PackageReferences.Single(p => p.Id == "GKit.Application");
    Assert.Equal("0.0.19", restored.Version);
    Assert.Empty(project.ProjectReferences);
  }

  [Fact]
  public void SwapIsANoOpForAPackageThatIsNotReferenced()
  {
    var project = ProjectFile.ParseText("a.csproj", Csproj);

    Assert.Null(project.SwapPackageForProject("GKit.Quartz", "../GKit.Quartz/GKit.Quartz.csproj"));
    Assert.Empty(project.ProjectReferences);
  }

  [Fact]
  public void FindsHintPathReferences()
  {
    var project = ProjectFile.ParseText("a.csproj", """
      <Project Sdk="Microsoft.NET.Sdk">
        <ItemGroup>
          <Reference Include="GKit.RENTRI">
            <HintPath>..\..\DotnetStack\GKit.RENTRI\bin\Debug\net10.0\GKit.RENTRI.dll</HintPath>
          </Reference>
        </ItemGroup>
      </Project>
      """);

    var (include, hintPath) = Assert.Single(project.HintPathReferences);
    Assert.Equal("GKit.RENTRI", include);
    Assert.Contains("bin", hintPath);
  }

  [Fact]
  public void SavingDoesNotInventAnXmlDeclaration()
  {
    using var workspace = new TempWorkspace();
    var path = workspace.WriteProject("App/App.csproj", "GKit.Application:0.0.19");

    var project = ProjectFile.Load(path);
    project.SetPackageVersion("GKit.Application", "0.1.0");
    project.Save();

    var text = File.ReadAllText(path);
    Assert.DoesNotContain("<?xml", text);
    Assert.Contains("""Version="0.1.0" """.TrimEnd(), text);
  }

  [Fact]
  public void SavingPreservesSurroundingLayout()
  {
    using var workspace = new TempWorkspace();
    var path = workspace.WriteProject("App/App.csproj", "GKit.Application:0.0.19", "MudBlazor:9.9.0");
    var before = File.ReadAllText(path);

    var project = ProjectFile.Load(path);
    project.RenamePackage("MudBlazor", "MudBlazor");
    project.Save();

    Assert.Equal(before.Replace("\r\n", "\n"), File.ReadAllText(path).Replace("\r\n", "\n"));
  }

  [Fact]
  public void RenamesAPackage()
  {
    var project = ProjectFile.ParseText("a.csproj", """
      <Project Sdk="Microsoft.NET.Sdk.Web">
        <ItemGroup>
          <PackageReference Include="GKit.MudBlazorExt" Version="0.0.32" />
        </ItemGroup>
      </Project>
      """);

    Assert.True(project.RenamePackage("GKit.MudBlazorExt", "GKit.UI.MudBlazorExt"));
    Assert.Equal("GKit.UI.MudBlazorExt", project.PackageReferences.Single().Id);
    Assert.Equal("0.0.32", project.PackageReferences.Single().Version);
  }
}
