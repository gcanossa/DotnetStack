using System.Data.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace GKit.MudBlazorExt;

public enum ManagedGridRowControlsVariant
{
  Expanded,
  Menu
}

public class ManagedGridRowControlDescriptor<T>
{
  public required string Text { get; set; }
  public required Func<CellContext<T>, Task> Action { get; set; }

  public string? Icon { get; set; }

  public Color Color { get; set; } = Color.Default;

  public Func<CellContext<T>, bool>? Disabled { get; set; }
}

[CascadingTypeParameter(nameof(T))]
public partial class ManagedGrid<T>
  where T : class
{
  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
  }

  public async Task WithLoading(Func<Task> fn)
  {
    try
    {
      _loading = true;

      await fn.Invoke();
    }
    finally
    {
      _loading = false;
    }
  }

  protected async Task ExportXlsAsync()
  {
    await WithLoading(async () =>
    {
      var title = Title ?? typeof(T).Name;

      var query = await ExportServerData(CancellationToken.None);

      query = QueryFilterExtensions.Where(query, _dataGrid.FilterDefinitions);
      query = QuerySortExtensions.OrderBy(query, _dataGrid.SortDefinitions.Values);

      using var ms = new MemoryStream();
      await query.ToXlsAsync(title, _dataGrid, ms);
      ms.Position = 0;
      await downloadFileService.DownloadFileFromStream(ms, $"{title}.xls");
    });
  }

  protected async Task RefreshDataAsync()
  {
    await _dataGrid.ReloadServerData();
    await InvokeAsync(StateHasChanged);
  }

  protected async Task<GridData<T>> ManagedLoadServerData(GridStateVirtualize<T> gridState, CancellationToken token)
  {
    try
    {
      return await LoadServerData(gridState, token);
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
  
  protected async Task RowClick(DataGridRowClickEventArgs<T> arg)
  {
    await OnRowClick.InvokeAsync(arg);
  }

  protected async Task OnRowControlClick(MouseEventArgs args, ManagedGridRowControlDescriptor<T> control,
    CellContext<T> context)
  {
    if(OnBeforeRowControlAction != null && !await OnBeforeRowControlAction.Invoke(control, context))
      return;

    await control.Action.Invoke(context);

    if (OnAfterRowControlAction != null)
      await OnAfterRowControlAction(control, context);
  }
}