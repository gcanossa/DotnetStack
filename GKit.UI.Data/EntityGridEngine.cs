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
public sealed class EntityGridEngine<T> where T : class
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

  public bool IsSharedContext => SharedContext is not null;

  private DbContext GetDbContext()
  {
    return SharedContext
           ?? ContextFactory?.Invoke()
           ?? throw new InvalidOperationException(
             $"Neither {nameof(SharedContext)} nor {nameof(ContextFactory)} is set");
  }

  private async Task PreambleAsync(DbContext ctx, bool shared, object[] entities)
  {
    if (!shared && entities.Length > 0)
    {
      try
      {
        ctx.AttachRange(entities);
      }
      catch (InvalidOperationException)
      {
        ctx.AttachRange(entities);
      }
    }

    if (shared)
      await _contextLock.WaitAsync();
  }

  private async Task EpilogueAsync(DbContext? ctx, bool shared)
  {
    if (ctx is null)
      return;

    if (shared)
      _contextLock.Release();
    else
      await ctx.DisposeAsync();
  }

  /// <summary>
  /// Runs <paramref name="action"/> against a context, attaching <paramref name="entities"/> first
  /// when the context is not shared, and disposing or unlocking afterwards.
  /// </summary>
  public async Task WithDbContextAsync(Func<DbContext, Task> action, params object[] entities)
  {
    DbContext? ctx = null;
    var shared = IsSharedContext;

    try
    {
      ctx = GetDbContext();
      await PreambleAsync(ctx, shared, entities);

      await action(ctx);
    }
    finally
    {
      await EpilogueAsync(ctx, shared);
    }
  }

  /// <inheritdoc cref="WithDbContextAsync(Func{DbContext, Task}, object[])"/>
  public async Task<TResult> WithDbContextAsync<TResult>(Func<DbContext, Task<TResult>> action, params object[] entities)
  {
    DbContext? ctx = null;
    var shared = IsSharedContext;

    try
    {
      ctx = GetDbContext();
      await PreambleAsync(ctx, shared, entities);

      return await action(ctx);
    }
    finally
    {
      await EpilogueAsync(ctx, shared);
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
        var source = QueryFactory?.Invoke(ctx)?.IgnoreQueryFilters()
                     ?? throw new InvalidOperationException($"{nameof(QueryFactory)} is not set");

        if (!IsSharedContext)
          source = source.AsNoTracking();
        else
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
  /// Builds the export query: the same base query with the grid's current filters and sorts, but
  /// unpaged. Global query filters are left in place, unlike <see cref="LoadAsync"/>.
  /// </summary>
  public IQueryable<T> BuildExportQuery(DbContext ctx, GridQuery<T> query)
  {
    var source = QueryFactory?.Invoke(ctx)
                 ?? throw new InvalidOperationException($"{nameof(QueryFactory)} is not set");

    return query.Apply(source);
  }

  /// <summary>
  /// Whether a provider exception represents a cancelled command rather than a real failure.
  /// </summary>
  internal static bool IsCancellation(DbException e) =>
    e.Message.Contains("aborted", StringComparison.InvariantCultureIgnoreCase) ||
    e.Message.Contains("cancelled", StringComparison.InvariantCultureIgnoreCase);
}
