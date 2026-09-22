using GKit.Cli;
using GKit.Cli.Commands;

namespace GKit.Tests.Cli;

public class ProgramEditorTests
{
  private const string Program = """
    using System.Globalization;
    using GKit.Application;

    Application.Wrap(args, () =>
    {
        var builder = WebApplication.CreateBuilder(args);

        var healthChecks = builder.Services.AddHealthChecks();
        healthChecks.AddDbContextCheck<ApplicationDbContext>("APP_DB");

        var app = builder.Build();

        return app;
    });
    """;

  [Fact]
  public void AddsAUsingAfterTheLastOne()
  {
    var editor = new ProgramEditor(Program);

    Assert.True(editor.AddUsing("GKit.Quartz"));

    var lines = editor.Text.Split('\n');
    Assert.Equal("using GKit.Application;", lines[1]);
    Assert.Equal("using GKit.Quartz;", lines[2]);
  }

  [Fact]
  public void AddingTheSameUsingTwiceIsANoOp()
  {
    var editor = new ProgramEditor(Program);

    Assert.True(editor.AddUsing("GKit.Quartz"));
    Assert.False(editor.AddUsing("GKit.Quartz"));
    Assert.Single(editor.Text.Split('\n').Where(p => p == "using GKit.Quartz;"));
  }

  [Fact]
  public void ServiceRegistrationsLandBeforeTheHostIsBuilt()
  {
    var editor = new ProgramEditor(Program);

    Assert.True(editor.AddServiceCall("builder.Services.AddGKitQuartz();"));

    var text = editor.Text;
    Assert.True(text.IndexOf("AddGKitQuartz", StringComparison.Ordinal) <
                text.IndexOf("builder.Build()", StringComparison.Ordinal));
  }

  [Fact]
  public void AppCallsLandBeforeTheHostIsReturned()
  {
    var editor = new ProgramEditor(Program);

    Assert.True(editor.AddAppCall("app.UseGKitQuartz();"));

    var text = editor.Text;
    Assert.True(text.IndexOf("UseGKitQuartz", StringComparison.Ordinal) <
                text.IndexOf("return app;", StringComparison.Ordinal));
    Assert.True(text.IndexOf("builder.Build()", StringComparison.Ordinal) <
                text.IndexOf("UseGKitQuartz", StringComparison.Ordinal));
  }

  [Fact]
  public void HealthChecksAppendToTheEndOfTheExistingChain()
  {
    var editor = new ProgramEditor(Program);

    Assert.True(editor.AddHealthCheck(".AddQuartzCheck(\"SCHEDULER\")"));

    var lines = editor.Text.Split('\n').Select(p => p.Trim()).ToList();
    var dbIndex = lines.IndexOf("healthChecks.AddDbContextCheck<ApplicationDbContext>(\"APP_DB\");");
    var quartzIndex = lines.IndexOf("healthChecks.AddQuartzCheck(\"SCHEDULER\");");

    Assert.True(dbIndex > 0);
    Assert.Equal(dbIndex + 1, quartzIndex);
  }

  [Fact]
  public void HealthChecksAreSkippedWhenTheAppWasScaffoldedWithoutThem()
  {
    var editor = new ProgramEditor("""
      Application.Wrap(args, () =>
      {
          var app = builder.Build();
          return app;
      });
      """);

    Assert.False(editor.AddHealthCheck(".AddQuartzCheck(\"SCHEDULER\")"));
    Assert.False(editor.Changed);
  }
}

public class AddFeatureTests
{
  private static readonly FeatureManifest Manifest = FeatureManifest.Load();

  private const string Program = """
    using GKit.Application;

    Application.Wrap(args, () =>
    {
        var builder = WebApplication.CreateBuilder(args);

        var healthChecks = builder.Services.AddHealthChecks();

        var app = builder.Build();

        return app;
    });
    """;

  private static TempWorkspace Scaffolded()
  {
    var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.WriteProject("App/App.csproj", "GKit.Application:0.1.3");
    workspace.Write("App/Program.cs", Program);
    return workspace;
  }

  [Fact]
  public void WiresAllFourEditsForACapability()
  {
    using var workspace = Scaffolded();

    var exit = AddCommand.Run(Workspace.Discover(workspace.Root), Manifest, "quartz", [], workspace.Root,
      TextWriter.Null);

    Assert.Equal(0, exit);

    var csproj = workspace.Read("App/App.csproj");
    var program = workspace.Read("App/Program.cs");

    Assert.Contains("""Include="GKit.Quartz" """.TrimEnd(), csproj);
    Assert.Contains("using GKit.Quartz;", program);
    Assert.Contains("builder.Services.AddGKitQuartz();", program);
    Assert.Contains("healthChecks.AddQuartzCheck(\"SCHEDULER\");", program);
    Assert.Contains("app.UseGKitQuartz();", program);
  }

  [Fact]
  public void ReusesTheVersionTheProjectAlreadyPinsGKitAt()
  {
    using var workspace = Scaffolded();

    AddCommand.Run(Workspace.Discover(workspace.Root), Manifest, "reporting", [], workspace.Root, TextWriter.Null);

    // Introducing a second version here is exactly the GKIT003 drift doctor reports.
    Assert.Contains("""<PackageReference Include="GKit.Reporting" Version="0.1.3" />""",
      workspace.Read("App/App.csproj"));
  }

  [Fact]
  public void AddingTwiceChangesNothingTheSecondTime()
  {
    using var workspace = Scaffolded();
    var discover = Workspace.Discover(workspace.Root);

    AddCommand.Run(discover, Manifest, "pdf", [], workspace.Root, TextWriter.Null);
    var after = workspace.Read("App/Program.cs");

    AddCommand.Run(Workspace.Discover(workspace.Root), Manifest, "pdf", [], workspace.Root, TextWriter.Null);

    Assert.Equal(after, workspace.Read("App/Program.cs"));
    Assert.Single(workspace.Read("App/App.csproj").Split("GKit.Pdf")[1..]);
  }

  [Fact]
  public void UnderCentralPackageManagementTheVersionGoesToThePropsFile()
  {
    using var workspace = Scaffolded();
    workspace.Write("Directory.Packages.props", """
      <Project>
        <PropertyGroup>
          <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
        </PropertyGroup>
        <ItemGroup Label="GKit">
        </ItemGroup>
      </Project>
      """);

    AddCommand.Run(Workspace.Discover(workspace.Root), Manifest, "quartz", [], workspace.Root, TextWriter.Null);

    Assert.DoesNotContain("""Include="GKit.Quartz" Version=""", workspace.Read("App/App.csproj"));
    Assert.Contains("GKit.Quartz", workspace.Read("Directory.Packages.props"));
  }

  [Fact]
  public void AnUnknownNameIsRejected()
  {
    using var workspace = Scaffolded();

    Assert.Equal(1, AddCommand.Run(Workspace.Discover(workspace.Root), Manifest, "nonsense", [], workspace.Root,
      TextWriter.Null));
  }

  [Fact]
  public void ACapabilityWithoutAHealthCheckDoesNotInventOne()
  {
    using var workspace = Scaffolded();

    AddCommand.Run(Workspace.Discover(workspace.Root), Manifest, "reporting", [], workspace.Root, TextWriter.Null);

    Assert.DoesNotContain("healthChecks.", workspace.Read("App/Program.cs"));
  }
}
