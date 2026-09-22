using GKit.Cli;
using GKit.Cli.Commands;

namespace GKit.Tests.Cli;

public class DoctorTests
{
  private static readonly FeatureManifest Manifest = FeatureManifest.Load();

  private static IReadOnlyList<Diagnostic> Analyze(TempWorkspace workspace) =>
    DoctorCommand.Analyze(Workspace.Discover(workspace.Root), Manifest);

  [Fact]
  public void FlagsHintPathReferencesAsErrors()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.Write("Data/Data.csproj", """
      <Project Sdk="Microsoft.NET.Sdk">
        <ItemGroup>
          <Reference Include="GKit.RENTRI">
            <HintPath>..\..\DotnetStack\GKit.RENTRI\bin\Debug\net10.0\GKit.RENTRI.dll</HintPath>
          </Reference>
        </ItemGroup>
      </Project>
      """);

    var diagnostic = Assert.Single(Analyze(workspace), p => p.Code == "GKIT001");
    Assert.Equal("error", diagnostic.Severity);
  }

  [Fact]
  public void FlagsRetiredPackages()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.WriteProject("App/App.csproj", "GKit.MudBlazorExt:0.0.32");

    var diagnostic = Assert.Single(Analyze(workspace), p => p.Code == "GKIT002");
    Assert.Contains("GKit.UI.MudBlazorExt", diagnostic.Message);
  }

  [Fact]
  public void FlagsVersionDriftAcrossProjects()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.WriteProject("A/A.csproj", "GKit.Application:0.0.17");
    workspace.WriteProject("B/B.csproj", "GKit.Application:0.0.19");

    var diagnostic = Assert.Single(Analyze(workspace), p => p.Code == "GKIT003");
    Assert.Contains("0.0.17", diagnostic.Message);
    Assert.Contains("0.0.19", diagnostic.Message);
  }

  [Fact]
  public void DoesNotFlagDriftWhenEveryProjectAgrees()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.WriteProject("A/A.csproj", "GKit.Application:0.0.19");
    workspace.WriteProject("B/B.csproj", "GKit.Application:0.0.19");

    Assert.DoesNotContain(Analyze(workspace), p => p.Code == "GKIT003");
  }

  [Fact]
  public void SuggestsCentralPackageManagementOnlyWhenMoreThanOneProjectPinsGKit()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.WriteProject("A/A.csproj", "GKit.Application:0.0.19");

    Assert.DoesNotContain(Analyze(workspace), p => p.Code == "GKIT004");

    workspace.WriteProject("B/B.csproj", "GKit.Quartz:0.0.19");
    Assert.Contains(Analyze(workspace), p => p.Code == "GKIT004");
  }

  [Fact]
  public void FlagsTheCommentedOutProjectReferenceToggle()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.Write("App/App.csproj", """
      <Project Sdk="Microsoft.NET.Sdk.Web">
        <ItemGroup>
          <PackageReference Include="GKit.Application" Version="0.0.19" />
      <!--    <ProjectReference Include="../../DotnetStack/GKit.Application/GKit.Application.csproj" />-->
        </ItemGroup>
      </Project>
      """);

    Assert.Contains(Analyze(workspace), p => p.Code == "GKIT006");
  }

  [Fact]
  public void FlagsASplitDataLayerMissingItsManifest()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.WriteProject("Acme.Data.EF.SqlServer/Acme.Data.EF.SqlServer.csproj");

    Assert.Contains(Analyze(workspace), p => p.Code == "GKIT007");

    workspace.Write("Acme.Data.EF.SqlServer/Manifest.cs", "public class Manifest { }");
    Assert.DoesNotContain(Analyze(workspace), p => p.Code == "GKIT007");
  }

  [Fact]
  public void ACleanSolutionProducesNothing()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.Write("version.json", "{}");
    workspace.WriteProject("App/App.csproj", "GKit.Application:0.1.0");

    Assert.Empty(Analyze(workspace));
  }
}
