using GKit.BlazorExt;
using GKit.Reporting;
using GKit.UI.Data;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace GKit.UI.RadzenExt;

/// <summary>
/// A RadzenDataGrid wired for server-side loading, with a toolbar, per-row controls and XLSX
/// export. Pages by default; set <see cref="Virtualize"/> to scroll instead.
/// </summary>
/// <remarks>
/// The MudBlazor twin of this component. Component name, type parameters and parameter names are
/// deliberately identical, so moving a page between adapters is a package swap plus a rewrite of
/// the column markup - nothing else.
/// </remarks>
public partial class ManagedGrid<T> : ComponentBase
  where T : class
{
  [Inject] protected DownloadFileService DownloadFileService { get; set; } = default!;
  [Inject] protected IUiIconSet IconSet { get; set; } = default!;
  [Inject] protected IGKitUiStrings Strings { get; set; } = default!;

  /// <summary>The application's house style for exports, registered by <c>AddGKitUiCore</c>.</summary>
  [Inject] protected XlsTheme ExportTheme { get; set; } = default!;

  /// <summary>
  /// What the export is actually styled with: the grid's own <see cref="ExportStyles"/> where it
  /// has one, the registered theme otherwise.
  /// </summary>
  protected XlsStyleOptions<T> EffectiveExportStyles =>
    ExportStyles ?? new XlsStyleOptions<T> { Theme = ExportTheme };

  protected RadzenDataGrid<T> Component = null!;

  /// <summary>The page Radzen last asked for; retained so Refresh and export can reuse it.</summary>
  protected GridQuery<T> LastQuery { get; set; } = new() { Count = 50 };

  /// <summary>
  /// The current page. Starts null deliberately: Radzen only fires its initial
  /// <c>LoadData</c> when it has no data yet, so seeding this with an empty list silently
  /// disables server-side loading.
  /// </summary>
  protected IEnumerable<T>? Data { get; set; }
  protected int Count { get; set; }

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
  [Parameter]
  public Func<GridQuery<T>, CancellationToken, Task<GridPage<T>>> LoadServerData { get; set; } = null!;

  [Parameter]
  public Func<CancellationToken, Task<IQueryable<T>>> ExportServerData { get; set; } = null!;

  [Parameter] public string? Title { get; set; }

  [Parameter] public RenderFragment? HeaderControls { get; set; }

  [Parameter] public ICollection<GridRowControl<T>> RowControls { get; set; } = [];

  [Parameter] public GridRowControlsVariant RowControlsVariant { get; set; } = GridRowControlsVariant.Expanded;

  [Parameter, EditorRequired] public RenderFragment Columns { get; set; } = null!;

  [Parameter] public Func<T, string> ToStringFunc { get; set; } = null!;

  [Parameter] public bool Exportable { get; set; }

  /// <summary>
  /// How the XLSX export is styled: a <see cref="XlsTheme"/> for the sheet's look and an optional
  /// per-cell resolver for the exceptions. Null uses the registered <see cref="XlsTheme"/>.
  /// </summary>
  [Parameter] public XlsStyleOptions<T>? ExportStyles { get; set; }

  [Parameter] public bool Refreshable { get; set; }

  [Parameter] public EventCallback<GridQuery<T>> OnLoadedServerData { get; set; }

  [Parameter] public EventCallback<T> OnRowClick { get; set; }

  [Parameter] public Func<GridRowControl<T>, RowContext<T>, Task<bool>>? OnBeforeRowControlAction { get; set; }

  [Parameter] public Func<GridRowControl<T>, RowContext<T>, Task>? OnAfterRowControlAction { get; set; }

  /// <summary>Radzen equivalent of MudBlazor's drag-drop column reordering.</summary>
  [Parameter] public bool DragDropColumnReordering { get; set; }

  /// <summary>
  /// Accepted for parity with the MudBlazor adapter. Radzen only offers on/off resizing, so any
  /// value other than none enables it.
  /// </summary>
  [Parameter] public bool AllowColumnResize { get; set; }

  [Parameter] public bool Loading { get; set; }

  /// <summary>
  /// Virtualise instead of paging. Off by default, matching the MudBlazor adapter.
  /// </summary>
  /// <remarks>
  /// Virtualised grids render no rows until JS has measured the viewport, so they are invisible
  /// to component tests and to prerendering. Paging is the safer default; turn this on where the
  /// scrolling UX matters more.
  /// </remarks>
  [Parameter] public bool Virtualize { get; set; }

  [Parameter] public int PageSize { get; set; } = 50;

  [Parameter] public bool MultiSelection { get; set; }
  [Parameter] public IList<T>? SelectedItems { get; set; }
  [Parameter] public EventCallback<IList<T>?> SelectedItemsChanged { get; set; }

  /// <summary>
  /// The grid's current filters, in neutral form. Pass this to report dialogs instead of Radzen's
  /// filter descriptors so they compile against either adapter.
  /// </summary>
  public GridFilterSet<T> Filters => LastQuery.Filters;

  /// <summary>The grid's current filters and sorts, unpaged.</summary>
  public GridQuery<T> CurrentQuery => new()
  {
    Filters = LastQuery.Filters,
    Sorts = LastQuery.Sorts,
    NativeSort = LastQuery.NativeSort
  };

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
      await query.ToXlsAsync(title, Component.ToExportColumns(Strings), ms, EffectiveExportStyles);
      ms.Position = 0;
      await DownloadFileService.DownloadFileFromStream(ms, $"{title}.xlsx");
    });
  }

  public async Task RefreshDataAsync()
  {
    await Component.Reload();
    await InvokeAsync(StateHasChanged);
  }

  /// <summary>
  /// Bridges Radzen's load callback to the neutral loader.
  /// </summary>
  /// <remarks>
  /// Radzen expects the handler to assign Data and Count rather than return a page, so the
  /// neutral <see cref="GridPage{T}"/> is unpacked here. Cancellation resolves to an empty page -
  /// see the note on <see cref="EntityGridEngine{T}.LoadAsync"/> about the exception type.
  /// </remarks>
  protected async Task ManagedLoadServerData(LoadDataArgs args)
  {
    var query = args.ToGridQuery<T>(PageSize);
    LastQuery = query;

    try
    {
      var page = await LoadServerData(query, CancellationToken.None);
      Data = page.Items;
      Count = page.TotalItems;
    }
    catch (OperationCanceledException)
    {
      Data = [];
      Count = 0;
    }
    finally
    {
      await OnLoadedServerData.InvokeAsync(query);
    }
  }

  protected string ResolveIcon(GridRowControl<T> control) =>
    control.IconOverride ?? (control.Icon is { } icon ? IconSet.Resolve(icon) : string.Empty);

  protected async Task OnRowControlClick(GridRowControl<T> control, T item, int rowIndex)
  {
    var context = new RowContext<T>(item, rowIndex);

    if (OnBeforeRowControlAction is not null && !await OnBeforeRowControlAction.Invoke(control, context))
      return;

    await control.Action.Invoke(context);

    if (OnAfterRowControlAction is not null)
      await OnAfterRowControlAction(control, context);
  }

  protected async Task RowSelect(T item)
  {
    await OnRowClick.InvokeAsync(item);
  }
}
