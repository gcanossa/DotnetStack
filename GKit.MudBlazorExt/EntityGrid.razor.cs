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
  protected async Task DeleteAsync(T entity)
  {
    var choice = await dialogService.ShowMessageBoxAsync(
    "Conferma Operazione",
    (MarkupString)(ToStringFunc != null ?
    $"Confermi di voler eliminare <strong>{ToStringFunc(entity)}</strong>?" : "Confermi l'operazione?"),
    yesText: "Ok", noText: "Annulla"
    );

    if (choice == true)
    {
      try
      {
        await using var ctx = ContextFactory.Invoke();
        
        try
        {
          ctx.Attach(entity);
        }
        catch (InvalidOperationException)
        {
          ctx.Attach(entity);
        }
        
        ctx.Remove(entity!);
        await ctx.SaveChangesAsync();
        snackbar.Add("Elemento eliminato con successo", Severity.Success);
      }
      catch (Exception e)
      {
        logger.LogError(e.Message);
        snackbar.Add("Impossibile eliminare l'elemento", Severity.Error);
      }

      await RefreshDataAsync();
    }

    await InvokeAsync(StateHasChanged);
  }

  protected async Task EditAsync(T entity)
  {
    using var ctx = ContextFactory.Invoke();
    
    try
    {
      ctx.Attach(entity);
    }
    catch (InvalidOperationException)
    {
      ctx.Attach(entity);
    }

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

      }
      catch (Exception e)
      {
        logger.LogError(e.Message);
        snackbar.Add("Impossibile modificare l'elemento", Severity.Error);
      }
    }

    await RefreshDataAsync();
  }

  public async Task RefreshDataAsync()
  {
    await Component.ReloadServerData();
    await InvokeAsync(StateHasChanged);
  }

  protected async Task NewAsync()
  {
    using var ctx = ContextFactory.Invoke();

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
      }
      catch (Exception e)
      {
        logger.LogError(e.Message);
        snackbar.Add("Impossibile aggiungere l'elemento", Severity.Error);
      }
    }

    await RefreshDataAsync();
  }

  protected async Task<GridData<T>> LoadEntityData(GridStateVirtualize<T> gridState, CancellationToken token)
  {
    try
    {
      using var ctx = ContextFactory.Invoke();
      var result = new GridData<T>();

      var query = QueryFactory?.Invoke(ctx)?.AsNoTracking()?.IgnoreQueryFilters() ??
                  throw new InvalidOperationException($"{nameof(QueryFactory)} is not set");

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
  }
}