using Bunit;
using GKit.UI;
using GKit.UI.Data;
using Microsoft.AspNetCore.Components;
using Test.Repo.UI.Shared;
using MudManagedGrid = GKit.UI.MudBlazorExt.ManagedGrid<Test.Repo.UI.Shared.Widget>;

namespace Test.Repo.UI;

/// <summary>
/// Both adapters page by default and virtualise only on request.
/// </summary>
/// <remarks>
/// Worth pinning down: the default decides whether a grid renders rows at all outside a browser,
/// and wiring the wrong server-data callback fails silently rather than loudly.
/// </remarks>
public class GridPagingModeTest : MudRenderTestBase
{
  private static readonly Widget[] _rows =
  [
    new() { Id = 1, Name = "Alpha", Quantity = 1 },
    new() { Id = 2, Name = "Beta", Quantity = 2 }
  ];

  private (IRenderedComponent<MudManagedGrid> Cut, List<GridQuery<Widget>> Queries) RenderGrid(bool virtualize)
  {
    var queries = new List<GridQuery<Widget>>();

    var cut = Render<MudManagedGrid>(p => p
      .Add(x => x.Title, "Paging mode")
      .Add(x => x.Virtualize, virtualize)
      .Add(x => x.PageSize, 25)
      .Add(x => x.LoadServerData, (q, _) =>
      {
        queries.Add(q);
        return Task.FromResult(new GridPage<Widget> { Items = _rows, TotalItems = _rows.Length });
      })
      .Add(x => x.Columns, (RenderFragment)(b => { })));

    return (cut, queries);
  }

  [Fact]
  public void MudGrid_DefaultsToPaging_AndRequestsTheFirstPage()
  {
    // The paged callback runs without a browser; the virtualised one would not.
    var (_, queries) = RenderGrid(virtualize: false);

    Assert.Single(queries);
    Assert.Equal(0, queries[0].StartIndex);
    Assert.Equal(25, queries[0].Count);
  }

  [Fact]
  public void MudGrid_DefaultsToPaging_RendersPagerControls()
  {
    var (cut, _) = RenderGrid(virtualize: false);

    // MudDataGrid always emits the pagination container; only its contents vary.
    Assert.NotEqual(string.Empty, cut.Find(".mud-table-pagination").TextContent.Trim());
  }

  [Fact]
  public void MudGrid_Virtualized_LeavesThePagerEmpty()
  {
    var (cut, _) = RenderGrid(virtualize: true);

    Assert.Equal(string.Empty, cut.Find(".mud-table-pagination").TextContent.Trim());
  }

  [Fact]
  public void MudGrid_PageSizeFlowsIntoTheNeutralQuery()
  {
    var queries = new List<GridQuery<Widget>>();

    Render<MudManagedGrid>(p => p
      .Add(x => x.PageSize, 7)
      .Add(x => x.LoadServerData, (q, _) =>
      {
        queries.Add(q);
        return Task.FromResult(GridPage<Widget>.Empty);
      })
      .Add(x => x.Columns, (RenderFragment)(b => { })));

    Assert.Equal(7, queries.Single().Count);
  }

  [Fact]
  public void MudGridState_PageIndexBecomesSkipTake()
  {
    // The translation that makes paged MudBlazor state fit the neutral skip/take window.
    var state = new global::MudBlazor.GridState<Widget> { Page = 3, PageSize = 20 };

    var query = GKit.UI.MudBlazorExt.MudGridStateExtensions.ToGridQuery(state);

    Assert.Equal(60, query.StartIndex);
    Assert.Equal(20, query.Count);
  }
}
