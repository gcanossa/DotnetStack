using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GKit.EntityFramework;

public class RevisionInterceptor : SaveChangesInterceptor
{
  private readonly List<IRevisionableEntity> _updateRegistrations = [];
  public void RegisterForUpdate(IRevisionableEntity entity)
  {
    if (!_updateRegistrations.Contains(entity))
    {
      _updateRegistrations.Add(entity);
    }
  }

  public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
  {
    if (eventData.Context is null) return result;

    foreach (var entry in eventData.Context.ChangeTracker.Entries())
    {
      if (entry is not { State: EntityState.Modified, Entity: IRevisionableEntity entity }
        || _updateRegistrations.Contains(entity)) continue;

      var newEntity = (IRevisionableEntity)entity.Clone();
      newEntity.Revision = entity.Revision.NewRevision();
      eventData.Context.Add(newEntity);

      entry.Reload();
      entity.Revision.IsCurrent = false;
    }
    return result;
  }

  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
  {
    var data = ValueTask.FromResult(SavingChanges(eventData, result));

    _updateRegistrations.Clear();

    return data;
  }
}