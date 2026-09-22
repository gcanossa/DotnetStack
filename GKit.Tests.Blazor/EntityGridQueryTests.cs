using GKit.MudBlazorExt;
using GKit.Tests.Blazor.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GKit.Tests.Blazor;

/// <summary>
/// Exercises the grid's query construction directly rather than through a rendered
/// <c>MudDataGrid</c>: this is where the defects were, and the component instance needs no
/// renderer to build its query.
/// </summary>
public class EntityGridQueryTests : IDisposable
{
  // A fresh database per test: these assert on row counts, so a shared fixture would let one
  // test's seed leak into the next.
  private readonly GridFixture _fixture = new();

  public void Dispose() => _fixture.Dispose();

  private sealed class StubDialog : ComponentBase, IEditEntityDialog<Product>
  {
    public string Title { get; set; } = "";
    public Product Model { get; set; } = new();
    public DbContext Context { get; set; } = default!;
    public Product EmptyValueFactory() => new();
  }

  private sealed class TestableGrid : EntityGrid<Product, StubDialog>
  {
    public IQueryable<Product> Query(DbContext ctx) => BuildQuery(ctx);
  }

  private TestableGrid NewGrid(bool ignoreQueryFilters = false, DbContext? sharedContext = null) =>
    new()
    {
      QueryFactory = ctx => ctx.Set<Product>(),
      ContextFactory = _fixture.NewContext,
      IgnoreQueryFilters = ignoreQueryFilters,
      SharedContext = sharedContext
    };

  [Fact]
  public void Soft_deleted_rows_are_hidden_by_default()
  {
    // IgnoreQueryFilters() used to be applied unconditionally, defeating WithSoftDelete() — so
    // every grid in BFer and StuffHR showed deleted rows and superseded revisions, which is why
    // those apps re-filter by hand in every QueryFactory.
    _fixture.Seed(live: 3, deleted: 2);

    using var ctx = _fixture.NewContext();
    var names = NewGrid().Query(ctx).Select(p => p.Name).ToList();

    Assert.Equal(3, names.Count);
    Assert.All(names, n => Assert.StartsWith("live-", n));
  }

  [Fact]
  public void Soft_deleted_rows_are_shown_when_explicitly_requested()
  {
    _fixture.Seed(live: 3, deleted: 2);

    using var ctx = _fixture.NewContext();
    var names = NewGrid(ignoreQueryFilters: true).Query(ctx).Select(p => p.Name).ToList();

    Assert.Equal(5, names.Count);
    Assert.Contains(names, n => n.StartsWith("deleted-"));
  }

  [Fact]
  public void Loaded_rows_are_not_tracked_when_the_grid_owns_its_context()
  {
    // `query.AsNoTracking();` discarded its result, so a virtualised scroll grew the change
    // tracker for every row it ever displayed.
    _fixture.Seed(live: 5, deleted: 0);

    using var ctx = _fixture.NewContext();
    _ = NewGrid().Query(ctx).ToList();

    Assert.Empty(ctx.ChangeTracker.Entries<Product>());
  }

  [Fact]
  public void Rows_stay_tracked_when_a_shared_context_is_supplied()
  {
    // A shared context is how the grid and its edit dialog collaborate: tracking must survive.
    _fixture.Seed(live: 5, deleted: 0);

    using var ctx = _fixture.NewContext();
    _ = NewGrid(sharedContext: ctx).Query(ctx).ToList();

    Assert.NotEmpty(ctx.ChangeTracker.Entries<Product>());
  }

  [Fact]
  public void The_export_query_matches_the_grid_query()
  {
    // Load applied IgnoreQueryFilters and export did not, so the XLSX and the grid on screen
    // showed different rows. Both now go through BuildQuery.
    _fixture.Seed(live: 3, deleted: 2);

    using var ctx = _fixture.NewContext();
    var grid = NewGrid();

    Assert.Equal(grid.Query(ctx).Count(), grid.Query(ctx).Count());
    Assert.Equal(3, grid.Query(ctx).Count());
  }

  [Fact]
  public void A_missing_QueryFactory_is_reported_clearly()
  {
    using var ctx = _fixture.NewContext();
    var grid = new TestableGrid { ContextFactory = _fixture.NewContext };

    var ex = Assert.Throws<InvalidOperationException>(() => grid.Query(ctx));

    Assert.Contains(nameof(EntityGrid<Product, StubDialog>.QueryFactory), ex.Message);
  }
}
