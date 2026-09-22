using GKit.Cli;
using GKit.Cli.Commands;

namespace GKit.Tests.Cli;

public class FeatureManifestTests
{
  private static readonly FeatureManifest Manifest = FeatureManifest.Load();

  [Fact]
  public void LoadsTheEmbeddedManifest() => Assert.NotEmpty(Manifest.GKitPackages);

  [Fact]
  public void ListsEveryUiAdapter() =>
    Assert.Equal(["mudblazor", "radzen", "none"], Manifest.UiNames);

  [Theory]
  [InlineData("quartz", ".AddQuartzCheck(\"SCHEDULER\")")]
  [InlineData("rentri", ".AddRentriCheck(\"RENTRI_API\")")]
  [InlineData("smartcard", ".AddSmartCardHostCheck(\"SMART_CARD\")")]
  public void CapabilitiesCarryTheirHealthCheck(string feature, string expected) =>
    Assert.Equal(expected, Manifest.GetFeature(feature)!.HealthCheck);

  [Fact]
  public void CapabilitiesWithoutAHealthCheckSaySo() =>
    Assert.Null(Manifest.GetFeature("reporting")!.HealthCheck);

  [Fact]
  public void EveryCapabilityPackageIsAKnownGKitPackage()
  {
    var known = Manifest.GKitPackages.ToHashSet(StringComparer.OrdinalIgnoreCase);

    foreach (var name in Manifest.FeatureNames)
    {
      foreach (var package in Manifest.GetFeature(name)!.Packages)
        Assert.Contains(package, known);
    }
  }

  [Fact]
  public void TheRetiredPackageIsNotAlsoOfferedAsCurrent()
  {
    foreach (var (retired, replacement) in Manifest.RetiredPackages)
    {
      Assert.DoesNotContain(retired, Manifest.GKitPackages);
      Assert.Contains(replacement.Replacement, Manifest.GKitPackages);
    }
  }

  [Fact]
  public void UnknownFeaturesReturnNull() => Assert.Null(Manifest.GetFeature("does-not-exist"));

  [Fact]
  public void CommaSeparatedFeaturesBecomeRepeatedFlags()
  {
    // dotnet new wants one --features per value; the proposal documents the comma form.
    var arguments = NewCommand.BuildArguments("app", "Acme.Erp", ["--ui", "radzen", "--features", "quartz,reporting"]);

    Assert.Equal(
      ["new", "gkit-app", "-n", "Acme.Erp", "--ui", "radzen", "--features", "quartz", "--features", "reporting"],
      arguments);
  }

  [Fact]
  public void ASingleFeatureIsPassedThroughUnchanged()
  {
    var arguments = NewCommand.BuildArguments("sln", "Acme", ["--features", "quartz"]);

    Assert.Equal(["new", "gkit-sln", "-n", "Acme", "--features", "quartz"], arguments);
  }

  [Fact]
  public void ReleaseArtifactsAreNamedAfterHeightAndBranch() =>
    Assert.Equal("Acme.Erp.113-main.zip", ReleaseCommand.ArtifactName("Acme.Erp", 113, "main"));
}
