using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace GKit.UI.Data;

/// <summary>
/// Loads grid pages from EF Core and owns the DbContext lifecycle around every grid operation.
/// </summary>
/// <remarks>
/// <para>
/// A grid runs in one of two modes. With a <see cref="SharedContext"/> every operation reuses that
/// context, serialised behind a lock because a DbContext is not thread-safe and grid loads overlap
/// freely with edits. Without one, each operation gets a fresh context from
/// <see cref="ContextFactory"/> and entities are attached into it explicitly.
/// </para>
/// <para>
/// This type has no dependency on any component library, which is what makes it testable without a
/// renderer.
/// </para>
/// </remarks>
public sealed class EntityGridEngine<T> : IDisposable where T : class
{
  private readonly SemaphoreSlim _contextLock = new(1, 1);

  /// <summary>Creates a context per operation. Required unless <see cref="SharedContext"/> is set.</summary>
  public Func<DbContext>? ContextFactory { get; set; }

  /// <summary>Builds the grid's base query. Required.</summary>
  public Func<DbContext, IQueryable<T>>? QueryFactory { get; set; }

  /// <summary>When set, every operation reuses this context instead of creating one.</summary>
  public DbContext? SharedContext { get; set; }

  /// <summary>Invoked after every load attempt, successful or not.</summary>
  public Func<GridQuery<T>, Task>? OnLoaded { get; set; }

  /// <summary>
  /// Strip EF global query filters from the grid's query. Off by default.
  /// </summary>
  /// <remarks>
  /// This used to be applied unconditionally, which defeated <c>WithSoftDelete()</c>: every grid
  /// showed deleted rows and superseded revisions, and callers had to re-filter by hand in each
  /// QueryFactory. Export did not apply it, so the spreadsheet and the screen disagreed. It is now
  /// opt-in and applies to both.
  /// </remarks>
  public bool IgnoreQueryFilters { get; set; }

  public bool IsSharedContext => SharedContext is not null;

  private DbContext GetDbContext()
  {
    return SharedContext
           ?? ContextFactory?.Invoke()
           ?? throw new InvalidOperationException(
             $"Neither {nameof(SharedContext)} nor {nameof(ContextFactory)} is set");
  }

  /// <summary>
  /// Prepares the context and reports whether the lock was taken.
  /// </summary>
  /// <remarks>
  /// The lock is taken first. Acquiring it last meant an attach failure sent the epilogue on to
  /// release a semaphore that was never acquired, turning one error into a SemaphoreFullException.
  /// </remarks>
  private async Task<bool> PreambleAsync(DbContext ctx, bool shared, object[] entities)
  {
    if (shared)
      await _contextLock.WaitAsync();

    if (!shared && entities.Length > 0)
    {
      // An already-tracked instance is the real cause of the attach failure this used to catch
      // and then retry with the identical call, which could only throw again.
      foreach (var entity in entities)
      {
        if (ctx.Entry(entity).State == EntityState.Detached)
          ctx.Attach(entity);
      }
    }

    return shared;
  }

  private async Task EpilogueAsync(DbContext? ctx, bool lockTaken)
  {
    if (lockTaken)
      _contextLock.Release();

    if (ctx is not null && !IsSharedContext)
      await ctx.DisposeAsync();
  }

  /// <summary>
  /// Runs <paramref name="action"/> against a context, attaching <paramref name="entities"/> first
  /// when the context is not shared, and disposing or unlocking afterwards.
  /// </summary>
  public async Task WithDbContextAsync(Func<DbContext, Task> action, params object[] entities)
  {
    DbContext? ctx = null;
    var lockTaken = false;

    try
    {
      ctx = GetDbContext();
      lockTaken = await PreambleAsync(ctx, IsSharedContext, entities);

      await action(ctx);
    }
    finally
    {
      await EpilogueAsync(ctx, lockTaken);
    }
  }

  /// <inheritdoc cref="WithDbContextAsync(Func{DbContext, Task}, object[])"/>
  public async Task<TResult> WithDbContextAsync<TResult>(Func<DbContext, Task<TResult>> action, params object[] entities)
  {
    DbContext? ctx = null;
    var lockTaken = false;

    try
    {
      ctx = GetDbContext();
      lockTaken = await PreambleAsync(ctx, IsSharedContext, entities);

      return await action(ctx);
    }
    finally
    {
      await EpilogueAsync(ctx, lockTaken);
    }
  }

  /// <summary>
  /// Loads one page, applying the query's filters and sorts.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Cancellation is expected here rather than exceptional: a virtualised grid abandons in-flight
  /// loads as the user scrolls or types into a filter. Both the managed cancellation exception and
  /// the provider-level abort that surfaces as a <see cref="DbException"/> resolve to an empty
  /// page. Any other database error propagates.
  /// </para>
  /// <para>
  /// The catch is on <see cref="OperationCanceledException"/>, not
  /// <see cref="TaskCanceledException"/>. EF Core's own <c>ThrowIfCancellationRequested</c> raises
  /// the base type, so catching only the derived one lets the common case through.
  /// </para>
  /// </remarks>
  public async Task<GridPage<T>> LoadAsync(GridQuery<T> query, CancellationToken token)
  {
    return await WithDbContextAsync(async ctx =>
    {
      try
      {
        var source = BuildQuery(ctx);

        if (IsSharedContext)
          ctx.ChangeTracker.Clear();

        source = query.ApplyFilters(source);

        var total = await source.CountAsync(token);

        source = query.ApplySorts(source);

        var items = await source.Skip(query.StartIndex).Take(query.Count).ToListAsync(token);

        return new GridPage<T> { Items = items, TotalItems = total };
      }
      catch (OperationCanceledException)
      {
        return GridPage<T>.Empty;
      }
      catch (DbException e) when (IsCancellation(e))
      {
        return GridPage<T>.Empty;
      }
      finally
      {
        if (OnLoaded is not null)
          await OnLoaded.Invoke(query);
      }
    });
  }

  /// <summary>
  /// The grid's base query, with <see cref="IgnoreQueryFilters"/> and tracking applied.
  /// </summary>
  /// <remarks>
  /// Shared by the grid and the export so the spreadsheet and the screen cannot disagree about
  /// which rows exist.
  /// </remarks>
  public IQueryable<T> BuildQuery(DbContext ctx)
  {
    var source = QueryFactory?.Invoke(ctx)
                 ?? throw new InvalidOperationException($"{nameof(QueryFactory)} is not set");

    if (IgnoreQueryFilters)
      source = source.IgnoreQueryFilters();

    // AsNoTracking is a pure function; its result used to be discarded, so every scroll grew the
    // change tracker without bound.
    if (!IsSharedContext)
      source = source.AsNoTracking();

    return source;
  }

  /// <summary>
  /// Builds the export query: the same base query with the grid's current filters and sorts, but
  /// unpaged.
  /// </summary>
  public IQueryable<T> BuildExportQuery(DbContext ctx, GridQuery<T> query) =>
    query.Apply(BuildQuery(ctx));

  /// <summary>
  /// Whether a provider exception represents a cancelled command rather than a real failure.
  /// </summary>
  internal static bool IsCancellation(DbException e) =>
    e.Message.Contains("aborted", StringComparison.InvariantCultureIgnoreCase) ||
    e.Message.Contains("cancelled", StringComparison.InvariantCultureIgnoreCase);

  public void Dispose()
  {
    _contextLock.Dispose();
    GC.SuppressFinalize(this);
  }
}
