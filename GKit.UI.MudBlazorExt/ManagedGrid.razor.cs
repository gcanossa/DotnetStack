using System.Data.Common;
using GKit.BlazorExt;
using GKit.UI.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace GKit.UI.MudBlazorExt;

/// <summary>
/// A MudDataGrid wired for server-side loading, with a toolbar, per-row controls and XLSX export.
/// Pages by default; set <see cref="Virtualize"/> to scroll instead.
/// </summary>
/// <remarks>
/// The grid owns presentation only. Ordering, export and - in <see cref="EntityGrid{T,TDialog}"/> -
/// the data and CRUD work live in <c>GKit.UI.Data</c>, against the neutral
/// <see cref="GridQuery{T}"/> model.
/// </remarks>
[CascadingTypeParameter(nameof(T))]
public partial class ManagedGrid<T> : ComponentBase
  where T : class
{
  [Inject] protected DownloadFileService DownloadFileService { get; set; } = default!;
  [Inject] protected IUiIconSet IconSet { get; set; } = default!;
  [Inject] protected IGKitUiStrings Strings { get; set; } = default!;

  protected MudDataGrid<T> Component = null!;

  /// <summary>Controls contributed by a derived grid, rendered before <see cref="RowControls"/>.</summary>
  protected ICollection<GridRowControl<T>> DefaultControls { get; } = [];

  protected IEnumerable<GridRowControl<T>> EffectiveRowControls => DefaultControls.Concat(RowControls);

  protected float ControlHeaderWidth => RowControlsVariant == GridRowControlsVariant.Menu
    ? 2.5f
    : 2.5f * EffectiveRowControls.Count();

  /// <summary>Toolbar content contributed by a derived grid, rendered after <see cref="HeaderControls"/>.</summary>
  protected RenderFragment? DefaultHeaderControls { get; set; }

  /// <summary>
  /// Loads a page of data. Required on <see cref="ManagedGrid{T}"/>;
  /// <see cref="EntityGrid{T,TDialog}"/> supplies its own and ignores anything passed here.
  /// </summary>
  /// <remarks>
  /// Not marked <c>EditorRequired</c> precisely because of that override - the attribute is
  /// inherited and would warn on every EntityGrid usage. The guard in
  /// <see cref="OnParametersSet"/> enforces it instead.
  /// </remarks>
  [Parameter]
  public Func<GridQuery<T>, CancellationToken, Task<GridPage<T>>> LoadServerData { get; set; } = null!;

  /// <summary>
  /// Supplies the unfiltered query for XLSX export. Only needed when <see cref="Exportable"/> is
  /// set on a plain <see cref="ManagedGrid{T}"/>.
  /// </summary>
  [Parameter]
  public Func<CancellationToken, Task<IQueryable<T>>> ExportServerData { get; set; } = null!;

  [Parameter] public string? Title { get; set; }

  [Parameter] public RenderFragment? HeaderControls { get; set; }

  [Parameter] public ICollection<GridRowControl<T>> RowControls { get; set; } = [];

  [Parameter] public GridRowControlsVariant RowControlsVariant { get; set; } = GridRowControlsVariant.Expanded;

  [Parameter, EditorRequired] public RenderFragment Columns { get; set; } = null!;

  [Parameter] public Func<T, string> ToStringFunc { get; set; } = null!;

  [Parameter] public bool Exportable { get; set; }

  [Parameter] public bool Refreshable { get; set; }

  [Parameter] public Func<T, int, string> RowClassFunc { get; set; } = null!;

  [Parameter] public EventCallback<GridQuery<T>> OnLoadedServerData { get; set; }

  [Parameter] public EventCallback<DataGridRowClickEventArgs<T>> OnRowClick { get; set; }

  [Parameter] public Func<GridRowControl<T>, RowContext<T>, Task<bool>>? OnBeforeRowControlAction { get; set; }

  [Parameter] public Func<GridRowControl<T>, RowContext<T>, Task>? OnAfterRowControlAction { get; set; }

  [Parameter] public bool DragDropColumnReordering { get; set; }

  [Parameter] public ResizeMode ColumnResizeMode { get; set; } = ResizeMode.None;

  [Parameter] public bool Loading { get; set; }

  /// <summary>
  /// Virtualise instead of paging. Off by default, matching the Radzen adapter.
  /// </summary>
  /// <remarks>
  /// Virtualised grids render no rows until JS has measured the viewport, so they are invisible
  /// to component tests and to prerendering. Paging is the safer default; turn this on where the
  /// scrolling UX matters more.
  /// </remarks>
  [Parameter] public bool Virtualize { get; set; }

  [Parameter] public int PageSize { get; set; } = 50;

  [Parameter] public bool MultiSelection { get; set; }
  [Parameter] public bool SelectOnRowClick { get; set; } = true;
  [Parameter] public HashSet<T>? SelectedItems { get; set; }
  [Parameter] public EventCallback<HashSet<T>?> SelectedItemsChanged { get; set; }
  [Parameter] public T? SelectedItem { get; set; }
  [Parameter] public EventCallback<T?> SelectedItemChanged { get; set; }

  /// <summary>
  /// The grid's current filters, in neutral form. Pass this to report dialogs instead of
  /// MudBlazor's filter definitions so they compile against either adapter.
  /// </summary>
  public GridFilterSet<T> Filters => Component.FilterDefinitions.ToGridFilterSet();

  /// <summary>The grid's current filters and sorts, unpaged.</summary>
  public GridQuery<T> CurrentQuery => Component.ToUnpagedGridQuery();

  protected override void OnParametersSet()
  {
    base.OnParametersSet();

    if (LoadServerData is null)
      throw new InvalidOperationException(
        $"{nameof(LoadServerData)} must be set on {nameof(ManagedGrid<T>)}<{typeof(T).Name}>.");
  }

  public async Task WithLoading(Func<Task> fn)
  {
    try
    {
      Loading = true;
      await fn.Invoke();
    }
    finally
    {
      Loading = false;
    }
  }

  protected virtual async Task ExportXlsAsync()
  {
    if (ExportServerData is null)
      throw new InvalidOperationException(
        $"{nameof(ExportServerData)} must be set to export from {nameof(ManagedGrid<T>)}<{typeof(T).Name}>.");

    await WithLoading(async () =>
    {
      var title = Title ?? typeof(T).Name;

      var query = await ExportServerData(CancellationToken.None);
      query = CurrentQuery.Apply(query);

      using var ms = new MemoryStream();
      await query.ToXlsAsync(title, Component.ToExportColumns(Strings), ms);
      ms.Position = 0;
      await DownloadFileService.DownloadFileFromStream(ms, $"{title}.xlsx");
    });
  }

  public async Task RefreshDataAsync()
  {
    await Component.ReloadServerData();
    await InvokeAsync(StateHasChanged);
  }

  /// <summary>
  /// Bridges MudBlazor's virtualised data callback to the neutral loader.
  /// </summary>
  /// <remarks>
  /// A virtualised grid abandons in-flight loads as the user scrolls or edits a filter, so
  /// cancellation is routine rather than exceptional and resolves to an empty page. Other
  /// database errors propagate. Note the catch is on <see cref="OperationCanceledException"/>
  /// rather than the derived <see cref="TaskCanceledException"/>, since EF Core raises the base.
  /// </remarks>
  protected Task<GridData<T>> ManagedLoadVirtualizedData(GridStateVirtualize<T> gridState, CancellationToken token) =>
    LoadAsync(gridState.ToGridQuery(), token);

  /// <summary>
  /// The paged counterpart of <see cref="ManagedLoadVirtualizedData"/>. MudBlazor exposes paged
  /// and virtualised server data through separate callbacks taking different state types.
  /// </summary>
  protected Task<GridData<T>> ManagedLoadPagedData(GridState<T> gridState, CancellationToken token) =>
    LoadAsync(gridState.ToGridQuery(), token);

  private async Task<GridData<T>> LoadAsync(GridQuery<T> query, CancellationToken token)
  {
    try
    {
      return (await LoadServerData(query, token)).ToGridData();
    }
    catch (OperationCanceledException)
    {
      return GridPage<T>.Empty.ToGridData();
    }
    catch (DbException e) when (
      e.Message.Contains("aborted", StringComparison.InvariantCultureIgnoreCase) ||
      e.Message.Contains("cancelled", StringComparison.InvariantCultureIgnoreCase))
    {
      return GridPage<T>.Empty.ToGridData();
    }
    finally
    {
      await OnLoadedServerData.InvokeAsync(query);
    }
  }

  protected async Task RowClick(DataGridRowClickEventArgs<T> arg)
  {
    await OnRowClick.InvokeAsync(arg);
  }

  protected string ResolveIcon(GridRowControl<T> control) =>
    control.IconOverride ?? (control.Icon is { } icon ? IconSet.Resolve(icon) : string.Empty);

  protected static RowContext<T> ToRowContext(CellContext<T> context) => new(context.Item, 0);

  protected async Task OnRowControlClick(MouseEventArgs args, GridRowControl<T> control, CellContext<T> cellContext)
  {
    var context = ToRowContext(cellContext);

    if (OnBeforeRowControlAction is not null && !await OnBeforeRowControlAction.Invoke(control, context))
      return;

    await control.Action.Invoke(context);

    if (OnAfterRowControlAction is not null)
      await OnAfterRowControlAction(control, context);
  }
}
