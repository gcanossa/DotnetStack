using GKit.Cli;
using GKit.Cli.Commands;

namespace GKit.Tests.Cli;

public class LinkTests
{
  private static readonly FeatureManifest Manifest = FeatureManifest.Load();

  /// <summary>Stands in for a DotnetStack checkout: GKit.&lt;name&gt;/GKit.&lt;name&gt;.csproj.</summary>
  private static string FakeCheckout(TempWorkspace workspace, params string[] packages)
  {
    foreach (var package in packages)
      workspace.Write($"checkout/{package}/{package}.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

    return Path.Combine(workspace.Root, "checkout");
  }

  [Fact]
  public void LinkSwapsPackagesForProjectsAndUnlinkPutsThemBack()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.WriteProject("App/App.csproj", "GKit.Application:0.0.19", "MudBlazor:9.9.0");
    var checkout = FakeCheckout(workspace, "GKit.Application");

    var link = LinkCommand.Run(Workspace.Discover(workspace.Root), Manifest, checkout, [], true, TextWriter.Null);
    Assert.Equal(0, link);

    var linked = workspace.Read("App/App.csproj");
    Assert.Contains("ProjectReference", linked);
    Assert.DoesNotContain("""Include="GKit.Application" Version""", linked);
    // A non GKit package is left alone.
    Assert.Contains("""<PackageReference Include="MudBlazor" Version="9.9.0" />""", linked);

    var unlink = LinkCommand.Unlink(Workspace.Discover(workspace.Root), true, TextWriter.Null);
    Assert.Equal(0, unlink);

    var restored = workspace.Read("App/App.csproj");
    Assert.DoesNotContain("ProjectReference", restored);
    Assert.Contains("""<PackageReference Include="GKit.Application" Version="0.0.19" />""", restored);
  }

  [Fact]
  public void LinkAddsTheReferencedProjectsToTheSolution()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.WriteProject("App/App.csproj", "GKit.Quartz:0.1.0");
    var checkout = FakeCheckout(workspace, "GKit.Quartz");

    LinkCommand.Run(Workspace.Discover(workspace.Root), Manifest, checkout, [], true, TextWriter.Null);
    Assert.Contains("GKit.Quartz.csproj", workspace.Read("Acme.slnx"));

    LinkCommand.Unlink(Workspace.Discover(workspace.Root), true, TextWriter.Null);
    Assert.DoesNotContain("GKit.Quartz.csproj", workspace.Read("Acme.slnx"));
  }

  [Fact]
  public void OnlyRestrictsWhichPackagesAreLinked()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.WriteProject("App/App.csproj", "GKit.Application:0.1.0", "GKit.Quartz:0.1.0");
    var checkout = FakeCheckout(workspace, "GKit.Application", "GKit.Quartz");

    LinkCommand.Run(Workspace.Discover(workspace.Root), Manifest, checkout, ["GKit.Quartz"], false, TextWriter.Null);

    var text = workspace.Read("App/App.csproj");
    Assert.Contains("""<PackageReference Include="GKit.Application" Version="0.1.0" />""", text);
    Assert.Contains("GKit.Quartz.csproj", text);
  }

  [Fact]
  public void LinkingTwiceIsRefused()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.WriteProject("App/App.csproj", "GKit.Application:0.1.0");
    var checkout = FakeCheckout(workspace, "GKit.Application");

    Assert.Equal(0, LinkCommand.Run(Workspace.Discover(workspace.Root), Manifest, checkout, [], false, TextWriter.Null));
    Assert.Equal(1, LinkCommand.Run(Workspace.Discover(workspace.Root), Manifest, checkout, [], false, TextWriter.Null));
  }

  [Fact]
  public void UnlinkWithoutASidecarFails()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();

    Assert.Equal(1, LinkCommand.Unlink(Workspace.Discover(workspace.Root), false, TextWriter.Null));
  }

  [Fact]
  public void APathThatIsNotACheckoutIsRejected()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.WriteProject("App/App.csproj", "GKit.Application:0.1.0");

    Assert.Equal(1, LinkCommand.Run(Workspace.Discover(workspace.Root), Manifest,
      Path.Combine(workspace.Root, "nothing-here"), [], false, TextWriter.Null));
  }
}
