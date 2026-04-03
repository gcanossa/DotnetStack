using System.Data.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace GKit.MudBlazorExt;

public record NewValueResult<N>(bool Canceled, N Value);

[CascadingTypeParameter(nameof(T))]
public partial class EntityGrid<T, TDialog> : ManagedGrid<T>
  where T : class
  where TDialog : IEditEntityDialog<T>, IComponent
{
  protected SemaphoreSlim _ctxLock = new SemaphoreSlim(1, 1);
  protected bool IsSharedContext()
  {
    return SharedContext is not null;
  }

  protected DbContext GetDbContext()
  {
    return SharedContext ?? ContextFactory.Invoke();
  }

  private async Task WithDbContextPreamble(DbContext ctx, bool shared, object[] entities)
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
    if(shared)
      await _ctxLock.WaitAsync();
  }

  private async Task WithDbContextEpilogue(DbContext? ctx, bool shared)
  {
    if (ctx != null)
    {
      if (shared)
        _ctxLock.Release();
      else
        await ctx.DisposeAsync();
    }
  }
  
  protected async Task WithDbContext(Func<DbContext, Task> action, params object[] entities)
  {
    DbContext? ctx = null;
    var shared = IsSharedContext();
    try
    {
      ctx = GetDbContext();
      await WithDbContextPreamble(ctx, shared, entities);
      
      await action(ctx);
    }
    finally
    {
      await WithDbContextEpilogue(ctx, shared);
    }
  }
  
  protected async Task<R> WithDbContextReturning<R>(Func<DbContext, Task<R>> action, params object[] entities)
  {
    DbContext? ctx = null;
    var shared = IsSharedContext();
    try
    {
      ctx = GetDbContext();
      await WithDbContextPreamble(ctx, shared, entities);
      
      return await action(ctx);
    }
    finally
    {
      await WithDbContextEpilogue(ctx, shared);
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
        logger.LogError(e.Message);
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
          logger.LogError(e.Message);
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
          logger.LogError("Error: {}", e.Message);
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

        var query = QueryFactory?.Invoke(ctx) ?? throw new InvalidOperationException($"{nameof(QueryFactory)} is not set");

        query = QueryFilterExtensions.Where(query, Component.FilterDefinitions);
        query = QuerySortExtensions.OrderBy(query, Component.SortDefinitions.Values);

        using var ms = new MemoryStream();
        await query.ToXlsAsync(title, Component, ms);
        ms.Position = 0;
        await downloadFileService.DownloadFileFromStream(ms, $"{title}.xls");
      });
    });
  }
  
  protected async Task<GridData<T>> LoadEntityData(GridStateVirtualize<T> gridState, CancellationToken token)
  {
    return await WithDbContextReturning<GridData<T>>(async ctx =>
    {
      try
      {
        var result = new GridData<T>();

        var query = QueryFactory?.Invoke(ctx)?.IgnoreQueryFilters() ??
                    throw new InvalidOperationException($"{nameof(QueryFactory)} is not set");
        if (!IsSharedContext())
          query.AsNoTracking();
        else
          ctx.ChangeTracker.Clear();

        query = QueryFilterExtensions.Where(query, gridState.FilterDefinitions);
        query = QuerySortExtensions.OrderBy(query, gridState.SortDefinitions);

        result.TotalItems = await query.CountAsync(token);

        result.Items = await query.Skip(gridState.StartIndex).Take(gridState.Count).ToListAsync(token);

        return result;
      }
      catch (TaskCanceledException)
      {
        return new GridData<T>
        {
          Items = [],
          TotalItems = 0
        };
      }
      catch (DbException e)
      {

        if (!e.Message.Contains("aborted", StringComparison.InvariantCultureIgnoreCase) &&
            !e.Message.Contains("cancelled", StringComparison.InvariantCultureIgnoreCase))
          throw;
      
        return new GridData<T>
        {
          Items = [],
          TotalItems = 0
        };
      }
      finally
      {
        await OnLoadedServerData.InvokeAsync(gridState);
      }
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