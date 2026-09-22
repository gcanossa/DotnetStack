using Microsoft.Extensions.Logging;

namespace GKit.UI.Data;

/// <summary>
/// Drives the create / edit / delete flows for an entity grid: open the dialog, save, report the
/// outcome. Library-agnostic - the dialog and notification shells arrive as abstractions.
/// </summary>
public sealed class EntityCrudOrchestrator<T>(
  EntityGridEngine<T> engine,
  IUiDialogs dialogs,
  IUiNotifier notifier,
  IGKitUiStrings strings,
  ILogger logger)
  where T : class
{
  /// <summary>Renders an entity for the delete confirmation.</summary>
  public Func<T, string>? ToStringFunc { get; set; }

  /// <summary>Seeds a new entity before the create dialog opens. May cancel the whole flow.</summary>
  public Func<Task<NewValueResult<T>>>? NewValueFactory { get; set; }

  public Func<T, bool, Task>? OnAfterNew { get; set; }
  public Func<T, Task<bool>>? OnBeforeEdit { get; set; }
  public Func<T, bool, Task>? OnAfterEdit { get; set; }
  public Func<T, Task<bool>>? OnBeforeDelete { get; set; }
  public Func<T, bool, Task>? OnAfterDelete { get; set; }

  /// <summary>
  /// Opens the create dialog and saves the result.
  /// </summary>
  public async Task NewAsync(Type dialogType)
  {
    await engine.WithDbContextAsync(async ctx =>
    {
      T newEntity = null!;

      if (NewValueFactory is not null)
      {
        var seeded = await NewValueFactory();
        if (seeded.Canceled)
          return;

        newEntity = seeded.Value;
        if (newEntity is not null)
          ctx.Attach(newEntity);
      }

      var result = await dialogs.ShowAsync(dialogType, strings.CreateItemTitle, new Dictionary<string, object?>
      {
        [nameof(IEditEntityDialog<T>.Context)] = ctx,
        [nameof(IEditEntityDialog<T>.Title)] = strings.CreateItemTitle,
        [nameof(IEditEntityDialog<T>.Model)] = newEntity
      });

      if (result.Canceled)
      {
        await InvokeAfter(OnAfterNew, newEntity!, true);
        return;
      }

      try
      {
        // When no factory seeded an entity the dialog produced one, so it still needs attaching.
        if (newEntity is null && result.Data is not null)
          ctx.Attach((T)result.Data);

        await ctx.SaveChangesAsync();
        notifier.Success(strings.ItemAdded);
        await InvokeAfter(OnAfterNew, newEntity!, false);
      }
      catch (Exception e)
      {
        logger.LogError(e, "Could not add entity of type {EntityType}", typeof(T).Name);
        notifier.Error(strings.ItemAddFailed);
        await InvokeAfter(OnAfterNew, newEntity!, true);
      }
    });
  }

  /// <summary>
  /// Opens the edit dialog for an existing entity and saves the result.
  /// </summary>
  public async Task EditAsync(Type dialogType, T entity)
  {
    if (OnBeforeEdit is not null && !await OnBeforeEdit(entity))
      return;

    await engine.WithDbContextAsync(async ctx =>
    {
      var result = await dialogs.ShowAsync(dialogType, strings.EditItemTitle, new Dictionary<string, object?>
      {
        [nameof(IEditEntityDialog<T>.Model)] = entity,
        [nameof(IEditEntityDialog<T>.Context)] = ctx,
        [nameof(IEditEntityDialog<T>.Title)] = strings.EditItemTitle
      });

      if (result.Canceled)
        return;

      try
      {
        await ctx.SaveChangesAsync();
        notifier.Success(strings.ItemUpdated);
        await InvokeAfter(OnAfterEdit, entity, false);
      }
      catch (Exception e)
      {
        logger.LogError(e, "Could not update entity of type {EntityType}", typeof(T).Name);
        notifier.Error(strings.ItemUpdateFailed);
        await InvokeAfter(OnAfterEdit, entity, true);
      }
    }, entity);
  }

  /// <summary>
  /// Confirms and deletes an entity.
  /// </summary>
  public async Task DeleteAsync(T entity)
  {
    var confirmed = OnBeforeDelete is not null
      ? await OnBeforeDelete(entity)
      : await ConfirmDeleteAsync(entity);

    if (!confirmed)
      return;

    await engine.WithDbContextAsync(async ctx =>
    {
      try
      {
        ctx.Remove(entity);
        await ctx.SaveChangesAsync();
        notifier.Success(strings.ItemDeleted);
        await InvokeAfter(OnAfterDelete, entity, false);
      }
      catch (Exception e)
      {
        logger.LogError(e, "Could not delete entity of type {EntityType}", typeof(T).Name);
        notifier.Error(strings.ItemDeleteFailed);
        await InvokeAfter(OnAfterDelete, entity, true);
      }
    }, entity);
  }

  /// <summary>
  /// The default delete confirmation: names the entity when a renderer is available.
  /// </summary>
  public Task<bool> ConfirmDeleteAsync(T entity)
  {
    var message = ToStringFunc is not null
      ? strings.ConfirmDelete(ToStringFunc(entity))
      : strings.ConfirmGeneric;

    return dialogs.ConfirmAsync(strings.ConfirmTitle, message, strings.Ok, strings.Cancel);
  }

  private static Task InvokeAfter(Func<T, bool, Task>? hook, T entity, bool canceled) =>
    hook?.Invoke(entity, canceled) ?? Task.CompletedTask;
}
