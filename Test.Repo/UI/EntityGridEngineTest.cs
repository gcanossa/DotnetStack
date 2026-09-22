using GKit.UI;
using GKit.UI.Data;
using Microsoft.EntityFrameworkCore;

namespace Test.Repo.UI;

/// <summary>
/// Covers the DbContext lifecycle and paging behaviour that used to live inside the MudBlazor
/// grid component, and so could not be exercised without a renderer.
/// </summary>
public class EntityGridEngineTest
{
  private class Widget
  {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Rank { get; set; }
  }

  private class WidgetContext(DbContextOptions<WidgetContext> options) : DbContext(options)
  {
    public DbSet<Widget> Widgets => Set<Widget>();
  }

  private static WidgetContext NewContext(string name) =>
    new(new DbContextOptionsBuilder<WidgetContext>().UseInMemoryDatabase(name).Options);

  private static WidgetContext Seeded(string name, int count)
  {
    var ctx = NewContext(name);
    if (!ctx.Widgets.Any())
    {
      ctx.AddRange(Enumerable.Range(1, count)
        .Select(i => new Widget { Id = i, Name = $"w{i:00}", Rank = count - i }));
      ctx.SaveChanges();
    }

    return ctx;
  }

  private static EntityGridEngine<Widget> Engine(string db, DbContext? shared = null) => new()
  {
    ContextFactory = () => NewContext(db),
    QueryFactory = ctx => ctx.Set<Widget>(),
    SharedContext = shared
  };

  [Fact]
  public async Task LoadAsync_PagesAndReportsUnpagedTotal()
  {
    var db = nameof(LoadAsync_PagesAndReportsUnpagedTotal);
    using var seed = Seeded(db, 25);

    var page = await Engine(db).LoadAsync(new GridQuery<Widget> { StartIndex = 10, Count = 5 }, default);

    Assert.Equal(25, page.TotalItems);
    Assert.Equal(5, page.Items.Count);
  }

  [Fact]
  public async Task LoadAsync_AppliesFiltersBeforeCountingTotal()
  {
    var db = nameof(LoadAsync_AppliesFiltersBeforeCountingTotal);
    using var seed = Seeded(db, 20);

    var query = new GridQuery<Widget>
    {
      StartIndex = 0,
      Count = 100,
      Filters = new GridFilterSet<Widget> { Native = q => q.Where(w => w.Rank < 5) }
    };

    var page = await Engine(db).LoadAsync(query, default);

    // The total must reflect the filter, not the whole table, or virtualisation scrolls into
    // empty space.
    Assert.Equal(5, page.TotalItems);
    Assert.Equal(5, page.Items.Count);
  }

  [Fact]
  public async Task LoadAsync_AppliesSorts()
  {
    var db = nameof(LoadAsync_AppliesSorts);
    using var seed = Seeded(db, 5);

    var query = new GridQuery<Widget>
    {
      StartIndex = 0,
      Count = 5,
      NativeSort = q => q.OrderBy(w => w.Rank)
    };

    var page = await Engine(db).LoadAsync(query, default);

    Assert.Equal([0, 1, 2, 3, 4], page.Items.Select(w => w.Rank));
  }

  [Fact]
  public async Task LoadAsync_CancelledToken_ReturnsEmptyPageRatherThanThrowing()
  {
    var db = nameof(LoadAsync_CancelledToken_ReturnsEmptyPageRatherThanThrowing);
    using var seed = Seeded(db, 5);

    using var cts = new CancellationTokenSource();
    await cts.CancelAsync();

    var page = await Engine(db).LoadAsync(new GridQuery<Widget> { Count = 5 }, cts.Token);

    // A virtualised grid abandons loads constantly; cancellation must not surface as an error.
    Assert.Empty(page.Items);
    Assert.Equal(0, page.TotalItems);
  }

  [Fact]
  public async Task LoadAsync_InvokesOnLoadedHook()
  {
    var db = nameof(LoadAsync_InvokesOnLoadedHook);
    using var seed = Seeded(db, 3);

    var calls = 0;
    var engine = Engine(db);
    engine.OnLoaded = _ => { calls++; return Task.CompletedTask; };

    await engine.LoadAsync(new GridQuery<Widget> { Count = 3 }, default);

    Assert.Equal(1, calls);
  }

  [Fact]
  public async Task LoadAsync_CancelledToken_StillInvokesOnLoadedHook()
  {
    var db = nameof(LoadAsync_CancelledToken_StillInvokesOnLoadedHook);
    using var seed = Seeded(db, 3);

    using var cts = new CancellationTokenSource();
    await cts.CancelAsync();

    var calls = 0;
    var engine = Engine(db);
    engine.OnLoaded = _ => { calls++; return Task.CompletedTask; };

    await engine.LoadAsync(new GridQuery<Widget> { Count = 3 }, cts.Token);

    Assert.Equal(1, calls);
  }

  [Fact]
  public async Task WithDbContextAsync_PerOperationContext_IsDisposedAfterUse()
  {
    var db = nameof(WithDbContextAsync_PerOperationContext_IsDisposedAfterUse);
    using var seed = Seeded(db, 1);

    DbContext? captured = null;
    await Engine(db).WithDbContextAsync(ctx =>
    {
      captured = ctx;
      return Task.CompletedTask;
    });

    Assert.NotNull(captured);
    Assert.Throws<ObjectDisposedException>(() => captured!.Set<Widget>().Any());
  }

  [Fact]
  public async Task WithDbContextAsync_SharedContext_IsReusedAndNotDisposed()
  {
    var db = nameof(WithDbContextAsync_SharedContext_IsReusedAndNotDisposed);
    using var shared = Seeded(db, 1);
    var engine = Engine(db, shared);

    DbContext? first = null;
    DbContext? second = null;

    await engine.WithDbContextAsync(ctx => { first = ctx; return Task.CompletedTask; });
    await engine.WithDbContextAsync(ctx => { second = ctx; return Task.CompletedTask; });

    Assert.Same(shared, first);
    Assert.Same(shared, second);
    Assert.True(shared.Set<Widget>().Any());
  }

  [Fact]
  public async Task WithDbContextAsync_SharedContext_SerialisesConcurrentCallers()
  {
    var db = nameof(WithDbContextAsync_SharedContext_SerialisesConcurrentCallers);
    using var shared = Seeded(db, 1);
    var engine = Engine(db, shared);

    var concurrent = 0;
    var maxConcurrent = 0;
    var gate = new object();

    async Task Body(DbContext _)
    {
      lock (gate)
      {
        concurrent++;
        maxConcurrent = Math.Max(maxConcurrent, concurrent);
      }

      await Task.Delay(20);

      lock (gate) { concurrent--; }
    }

    await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => engine.WithDbContextAsync(Body)));

    // A DbContext is not thread-safe, so overlapping grid loads and edits must not interleave.
    Assert.Equal(1, maxConcurrent);
  }

  [Fact]
  public async Task WithDbContextAsync_ReleasesSharedLockWhenBodyThrows()
  {
    var db = nameof(WithDbContextAsync_ReleasesSharedLockWhenBodyThrows);
    using var shared = Seeded(db, 1);
    var engine = Engine(db, shared);

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
      engine.WithDbContextAsync(_ => throw new InvalidOperationException("boom")));

    // If the lock leaked, this would deadlock rather than complete.
    await engine.WithDbContextAsync(_ => Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(5));
  }

  [Fact]
  public async Task WithDbContextAsync_NoContextSource_Throws()
  {
    var engine = new EntityGridEngine<Widget> { QueryFactory = ctx => ctx.Set<Widget>() };

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
      engine.WithDbContextAsync(_ => Task.CompletedTask));
  }

  [Fact]
  public void BuildExportQuery_AppliesFiltersAndSortsButNotPaging()
  {
    var db = nameof(BuildExportQuery_AppliesFiltersAndSortsButNotPaging);
    using var ctx = Seeded(db, 10);

    var query = new GridQuery<Widget>
    {
      StartIndex = 0,
      Count = 2,
      Filters = new GridFilterSet<Widget> { Native = q => q.Where(w => w.Rank < 6) },
      NativeSort = q => q.OrderBy(w => w.Rank)
    };

    var exported = Engine(db).BuildExportQuery(ctx, query).ToList();

    // Export covers the whole filtered set, not just the visible page.
    Assert.Equal(6, exported.Count);
    Assert.Equal([0, 1, 2, 3, 4, 5], exported.Select(w => w.Rank));
  }
}
