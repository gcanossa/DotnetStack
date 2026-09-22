using GKit.UI.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GKit.UI.MudBlazorExt;

/// <summary>
/// A <see cref="ManagedGrid{T}"/> bound to an EF Core entity, with create / edit / delete wired to
/// a dialog of type <typeparamref name="TDialog"/>.
/// </summary>
/// <remarks>
/// This type is presentation and parameter plumbing only. The DbContext lifecycle, paging and CRUD
/// orchestration live in <see cref="EntityGridEngine{T}"/> and
/// <see cref="EntityCrudOrchestrator{T}"/>, neither of which references a component library.
/// </remarks>
[CascadingTypeParameter(nameof(T))]
public partial class EntityGrid<T, TDialog> : ManagedGrid<T>
  where T : class
  where TDialog : IEditEntityDialog<T>, IComponent
{
  [Inject] protected IUiDialogs Dialogs { get; set; } = default!;
  [Inject] protected IUiNotifier Notifier { get; set; } = default!;
  [Inject] protected ILogger<EntityGrid<T, TDialog>> Logger { get; set; } = default!;

  protected EntityGridEngine<T> Engine = null!;
  protected EntityCrudOrchestrator<T> Crud = null!;

  [Parameter] public Func<Task<NewValueResult<T>>>? NewValueFactory { get; set; }

  [Parameter, EditorRequired] public Func<DbContext, IQueryable<T>> QueryFactory { get; set; } = null!;

  [Parameter, EditorRequired] public Func<DbContext> ContextFactory { get; set; } = null!;

  [Parameter] public DbContext? SharedContext { get; set; }

  /// <summary>
  /// Strip EF global query filters from the grid's query. Off by default, and applied to the
  /// export as well so both show the same rows.
  /// </summary>
  /// <remarks>
  /// This used to be unconditional, which defeated <c>WithSoftDelete()</c>: deleted rows and
  /// superseded revisions appeared in every grid.
  /// </remarks>
  [Parameter] public bool IgnoreQueryFilters { get; set; }

  [Parameter] public Func<RowContext<T>, bool>? EditRowDisable { get; set; }
  [Parameter] public Func<RowContext<T>, bool>? DeleteRowDisable { get; set; }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();

    Engine = new EntityGridEngine<T>
    {
      ContextFactory = ContextFactory,
      QueryFactory = QueryFactory,
      SharedContext = SharedContext,
      IgnoreQueryFilters = IgnoreQueryFilters,
      OnLoaded = query => OnLoadedServerData.InvokeAsync(query)
    };

    Crud = new EntityCrudOrchestrator<T>(Engine, Dialogs, Notifier, Strings, Logger)
    {
      NewValueFactory = NewValueFactory,
      ToStringFunc = ToStringFunc,
      OnAfterNew = OnAfterNew,
      OnBeforeEdit = OnBeforeEdit,
      OnAfterEdit = OnAfterEdit,
      OnBeforeDelete = OnBeforeDelete,
      OnAfterDelete = OnAfterDelete
    };

    LoadServerData = Engine.LoadAsync;

    DefaultControls.Add(new GridRowControl<T>
    {
      Text = Strings.Edit,
      Action = ctx => EditAsync(ctx.Item),
      Icon = UiIcon.Edit,
      Disabled = ctx => EditRowDisable?.Invoke(ctx) ?? false
    });

    DefaultControls.Add(new GridRowControl<T>
    {
      Text = Strings.Delete,
      Action = ctx => DeleteAsync(ctx.Item),
      Icon = UiIcon.Delete,
      Color = UiColor.Error,
      Disabled = ctx => DeleteRowDisable?.Invoke(ctx) ?? false
    });
  }

  protected override void OnParametersSet()
  {
    base.OnParametersSet();

    if (Engine is null)
      return;

    // Parameters can change after initialisation; keep the engine in step.
    Engine.ContextFactory = ContextFactory;
    Engine.QueryFactory = QueryFactory;
    Engine.SharedContext = SharedContext;
    Engine.IgnoreQueryFilters = IgnoreQueryFilters;

    Crud.NewValueFactory = NewValueFactory;
    Crud.ToStringFunc = ToStringFunc;
  }

  protected async Task NewAsync()
  {
    await Crud.NewAsync(typeof(TDialog));
    await RefreshDataAsync();
  }

  protected async Task EditAsync(T entity)
  {
    await Crud.EditAsync(typeof(TDialog), entity);
    await RefreshDataAsync();
  }

  protected async Task DeleteAsync(T entity)
  {
    await Crud.DeleteAsync(entity);
    await RefreshDataAsync();
    await InvokeAsync(StateHasChanged);
  }

  /// <summary>
  /// Exports the filtered set. Unlike the grid load this keeps global query filters in place.
  /// </summary>
  protected override async Task ExportXlsAsync()
  {
    await Engine.WithDbContextAsync(async ctx =>
    {
      await WithLoading(async () =>
      {
        var title = Title ?? typeof(T).Name;

        var query = Engine.BuildExportQuery(ctx, CurrentQuery);

        using var ms = new MemoryStream();
        await query.ToXlsAsync(title, Component.ToExportColumns(Strings), ms);
        ms.Position = 0;
        await DownloadFileService.DownloadFileFromStream(ms, $"{title}.xlsx");
      });
    });
  }

  protected virtual Task OnAfterNew(T entity, bool canceled) => Task.CompletedTask;

  protected virtual Task<bool> OnBeforeEdit(T entity) => Task.FromResult(true);

  protected virtual Task OnAfterEdit(T entity, bool canceled) => Task.CompletedTask;

  protected virtual Task<bool> OnBeforeDelete(T entity) => Crud.ConfirmDeleteAsync(entity);

  protected virtual Task OnAfterDelete(T entity, bool canceled) => Task.CompletedTask;
}
