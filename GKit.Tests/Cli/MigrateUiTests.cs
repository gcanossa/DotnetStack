using GKit.Cli;
using GKit.Cli.Commands;

namespace GKit.Tests.Cli;

public class MigrateUiTests
{
  private static readonly FeatureManifest Manifest = FeatureManifest.Load();

  private static MigrateUiCommand.Result Analyze(TempWorkspace workspace, bool apply = false) =>
    MigrateUiCommand.Analyze(Workspace.Discover(workspace.Root), Manifest, apply);

  [Fact]
  public void RenamesTheNamespaceAndTheTypes()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.Write("App/Grid.razor.cs", """
      using GKit.MudBlazorExt;

      public class Grid
      {
        public GridStateVirtualize<Widget> State { get; set; } = new();
        public CellContext<Widget> Context { get; set; } = null!;
      }
      """);

    Analyze(workspace, apply: true);

    var text = workspace.Read("App/Grid.razor.cs");
    Assert.Contains("using GKit.UI.MudBlazorExt;", text);
    Assert.Contains("GridQuery<Widget>", text);
    Assert.Contains("RowContext<Widget>", text);
    Assert.DoesNotContain("GridStateVirtualize", text);
  }

  [Fact]
  public void WithoutApplyNothingIsWritten()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.Write("App/Grid.razor.cs", "using GKit.MudBlazorExt;\n");

    var result = Analyze(workspace);

    Assert.NotEmpty(result.Changes);
    Assert.Equal("using GKit.MudBlazorExt;\n", workspace.Read("App/Grid.razor.cs").Replace("\r\n", "\n"));
  }

  [Fact]
  public void RenamesTheRetiredPackageReference()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.WriteProject("App/App.csproj", "GKit.MudBlazorExt:0.0.32");

    var result = Analyze(workspace, apply: true);

    Assert.Equal(1, result.PackagesRenamed);
    Assert.Contains("GKit.UI.MudBlazorExt", workspace.Read("App/App.csproj"));
  }

  [Fact]
  public void ReportsMethodCallsInGridPropertyExpressionsInsteadOfRewritingThem()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    // The exact shape section 9.2 of the UI proposal calls out, from BFer's CompanyGrid.
    var original = """
      <ManagedGridColumnDescriptor Property="x => x.MainSite()!.AddressNumber" Title="N." />
      """;
    workspace.Write("App/CompanyGrid.razor", original);

    var result = Analyze(workspace, apply: true);

    var report = Assert.Single(result.Reports);
    Assert.Contains("Dynamic LINQ", report.Message);
    Assert.Equal(1, report.Line);
    // Reported, not rewritten.
    Assert.Equal(original, workspace.Read("App/CompanyGrid.razor"));
  }

  [Fact]
  public void ReportsFilterDefinitionCallSites()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.Write("App/Report.razor", """
      @code {
        public IEnumerable<IFilterDefinition<Movement>> FilterDefinitions { get; set; } = [];
        private IQueryable<Movement> Apply(IQueryable<Movement> q) => QueryFilterExtensions.Where(q, FilterDefinitions);
      }
      """);

    var result = Analyze(workspace);

    Assert.Contains(result.Reports, p => p.Message.Contains("GridFilterSet"));
    Assert.Contains(result.Reports, p => p.Message.Contains("ApplyFilters"));
  }

  [Fact]
  public void LeavesAnAlreadyMigratedProjectAlone()
  {
    using var workspace = new TempWorkspace();
    workspace.WriteSolution();
    workspace.Write("App/Grid.razor.cs", "using GKit.UI.MudBlazorExt;\n\npublic class Grid { }\n");
    workspace.WriteProject("App/App.csproj", "GKit.UI.MudBlazorExt:0.1.0");

    var result = Analyze(workspace);

    Assert.Empty(result.Changes);
    Assert.Empty(result.Reports);
  }
}
