using System.Data.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace GKit.MudBlazorExt;

public record NewValueResult<N>(bool Canceled, N Value);

[CascadingTypeParameter(nameof(T))]
public partial class EntityGrid<T, TDialog> : ManagedGrid<T>, IDisposable
  where T : class
  where TDialog : IEditEntityDialog<T>, IComponent
{
  protected SemaphoreSlim _ctxLock = new SemaphoreSlim(1, 1);

  public void Dispose()
  {
    _ctxLock.Dispose();
    GC.SuppressFinalize(this);
  }
  protected bool IsSharedContext()
  {
    return SharedContext is not null;
  }

  protected DbContext GetDbContext()
  {
    return SharedContext ?? ContextFactory.Invoke();
  }

  private async Task<bool> WithDbContextPreamble(DbContext ctx, bool shared, object[] entities)
  {
    // Take the lock first: releasing it in the epilogue after an attach failure used to throw
    // SemaphoreFullException on a semaphore that was never acquired.
    if (shared)
      await _ctxLock.WaitAsync();

    if (!shared && entities.Length > 0)
    {
      // The previous code caught InvalidOperationException and retried the identical call,
      // which could only throw again. An already-tracked instance is the real cause.
      foreach (var entity in entities)
      {
        if (ctx.Entry(entity).State == EntityState.Detached)
          ctx.Attach(entity);
      }
    }

    return shared;
  }

  private async Task WithDbContextEpilogue(DbContext? ctx, bool lockTaken)
  {
    if (lockTaken)
      _ctxLock.Release();

    if (ctx != null && !IsSharedContext())
      await ctx.DisposeAsync();
  }
  
  protected async Task WithDbContext(Func<DbContext, Task> action, params object[] entities)
  {
    DbContext? ctx = null;
    var lockTaken = false;
    try
    {
      ctx = GetDbContext();
      lockTaken = await WithDbContextPreamble(ctx, IsSharedContext(), entities);

      await action(ctx);
    }
    finally
    {
      await WithDbContextEpilogue(ctx, lockTaken);
    }
  }
  
  protected async Task<R> WithDbContextReturning<R>(Func<DbContext, Task<R>> action, params object[] entities)
  {
    DbContext? ctx = null;
    var lockTaken = false;
    try
    {
      ctx = GetDbContext();
      lockTaken = await WithDbContextPreamble(ctx, IsSharedContext(), entities);

      return await action(ctx);
    }
    finally
    {
      await WithDbContextEpilogue(ctx, lockTaken);
    }
  }
  
  protected async Task DeleteAsync(T entity)
  {
    if(!await OnBeforeDelete(entity))
      return;

    await WithDbContext(async ctx =>
    {
      try
      {
        ctx.Remove(entity!);
        await ctx.SaveChangesAsync();
        snackbar.Add("Elemento eliminato con successo", Severity.Success);
        await OnAfterDelete(entity, false);
      }
      catch (Exception e)
      {
        logger.LogError(e, "Unable to delete {Entity}", typeof(T).Name);
        snackbar.Add("Impossibile eliminare l'elemento", Severity.Error);
        await OnAfterDelete(entity, true);
      }
    }, entity);
    
    await RefreshDataAsync();

    await InvokeAsync(StateHasChanged);
  }

  protected async Task EditAsync(T entity)
  {
    if(!await OnBeforeEdit(entity))
      return;
    
    await WithDbContext(async ctx =>
    {
      var dialog = await dialogService.ShowAsync<TDialog>("Modifica Elemento", new DialogParameters<TDialog> {
        {p => p.Model, entity},
        {p => p.Context, ctx},
        {p => p.Title, (object)"Modifica Elemento"}
      });

      var shouldSave = await dialog.Result;
      if (shouldSave != null && !shouldSave.Canceled)
      {
        try
        {
          await ctx.SaveChangesAsync();
          snackbar.Add("Elemento modificato con successo", Severity.Success);
          await OnAfterEdit(entity, false);
        }
        catch (Exception e)
        {
          logger.LogError(e, "Unable to edit {Entity}", typeof(T).Name);
          snackbar.Add("Impossibile modificare l'elemento", Severity.Error);
          await OnAfterEdit(entity, true);
        }
      }
    }, entity);

    await RefreshDataAsync();
  }

  protected async Task NewAsync()
  {
    await WithDbContext(async ctx =>
    {
      T newEntity = null!;
      if (NewValueFactory != null)
      {
        var result = await NewValueFactory();
        if (result.Canceled)
          return;

        newEntity = result.Value;
        if (newEntity != null)
          ctx.Attach(newEntity);
      }

      var dialog = await dialogService.ShowAsync<TDialog>("Crea Elemento", new DialogParameters<TDialog> {
        {p => p.Context, ctx},
        {p => p.Title, (object)"Crea Elemento"},
        {p => p.Model, newEntity}
      });

      var shouldSave = await dialog.Result;
      if (shouldSave != null && !shouldSave.Canceled)
      {
        try
        {
          if (newEntity == null)
            ctx.Attach((T)shouldSave.Data!);

          await ctx.SaveChangesAsync();
          snackbar.Add("Elemento aggiunto con successo", Severity.Success);
          await OnAfterNew(newEntity!, false);
        }
        catch (Exception e)
        {
          logger.LogError(e, "Unable to add {Entity}", typeof(T).Name);
          snackbar.Add("Impossibile aggiungere l'elemento", Severity.Error);
          await OnAfterNew(newEntity!, true);
        }
      }
      else
      {
        await OnAfterNew(newEntity!, true);
      }
    });

    await RefreshDataAsync();
  }

  protected override async Task ExportXlsAsync()
  {
    await WithDbContext(async ctx =>
    {
      await WithLoading(async () =>
      {
        var title = Title ?? typeof(T).Name;

        var query = BuildQuery(ctx);

        query = QueryFilterExtensions.Where(query, Component.FilterDefinitions);
        query = QuerySortExtensions.OrderBy(query, Component.SortDefinitions.Values);

        using var ms = new MemoryStream();
        await query.ToXlsAsync(title, Component, ms);
        ms.Position = 0;
        await downloadFileService.DownloadFileFromStream(ms, $"{title}.xlsx");
      });
    });
  }
  
  /// <summary>
  /// Builds the grid's query.
  /// <para>
  /// <c>IgnoreQueryFilters()</c> used to be applied unconditionally, which defeated
  /// <c>GKit.EntityFramework.WithSoftDelete()</c> — every grid showed deleted rows and
  /// superseded revisions, and consumers had to re-filter by hand in every QueryFactory.
  /// Export did *not* apply it, so the XLSX had a different row set than the grid on screen.
  /// </para>
  /// </summary>
  protected IQueryable<T> BuildQuery(DbContext ctx)
  {
    var query = QueryFactory?.Invoke(ctx)
                ?? throw new InvalidOperationException($"{nameof(QueryFactory)} is not set");

    if (IgnoreQueryFilters)
      query = query.IgnoreQueryFilters();

    // AsNoTracking is a pure function; the result used to be discarded, so every virtualised
    // scroll grew the change tracker without bound.
    if (!IsSharedContext())
      query = query.AsNoTracking();

    return query;
  }

  protected async Task<GridData<T>> LoadEntityData(GridStateVirtualize<T> gridState, CancellationToken token)
  {
    return await WithDbContextReturning<GridData<T>>(async ctx =>
    {
      var result = new GridData<T>();

      var query = BuildQuery(ctx);

      if (IsSharedContext())
        ctx.ChangeTracker.Clear();

      query = QueryFilterExtensions.Where(query, gridState.FilterDefinitions);
      query = QuerySortExtensions.OrderBy(query, gridState.SortDefinitions);

      result.TotalItems = await query.CountAsync(token);

      result.Items = await query.Skip(gridState.StartIndex).Take(gridState.Count).ToListAsync(token);

      return result;
    });
  }

  protected virtual Task OnAfterNew(T entity, bool canceled)
  {
    return Task.CompletedTask;
  }
  
  protected virtual Task<bool> OnBeforeEdit(T entity)
  {
    return Task.FromResult(true);
  }
  
  protected virtual Task OnAfterEdit(T entity, bool canceled)
  {
    return Task.CompletedTask;
  }
  
  protected virtual async Task<bool> OnBeforeDelete(T entity)
  {
    return (await dialogService.ShowMessageBoxAsync(
      "Conferma Operazione",
      (MarkupString)(ToStringFunc != null ?
        $"Confermi di voler eliminare <strong>{ToStringFunc(entity)}</strong>?" : "Confermi l'operazione?"),
      yesText: "Ok", noText: "Annulla"
    )) ?? false;
  }
  
  protected virtual Task OnAfterDelete(T entity, bool canceled)
  {
    return Task.CompletedTask;
  }
}