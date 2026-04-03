using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GKit.EntityFramework;

public class RevisionInterceptor : SaveChangesInterceptor
{
  private readonly List<IRevisionableEntity> _updateRegistrations = [];
  private readonly List<object> _referenceUpdateRegistrations = [];
  
  public void RegisterForUpdate(IRevisionableEntity entity)
  {
    if (!_updateRegistrations.Contains(entity))
    {
      _updateRegistrations.Add(entity);
    }
  }
  public void RegisterForRevisionReferenceUpdate(object entity)
  {
    if (!_referenceUpdateRegistrations.Contains(entity))
    {
      _referenceUpdateRegistrations.Add(entity);
    }
  }

  public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
  {
    if (eventData.Context is null) return result;

    var entries = eventData.Context.ChangeTracker.Entries().ToList();
    foreach (var entry in entries)
    {
      if (entry is not { State: EntityState.Modified, Entity: IRevisionableEntity entity }
        || _updateRegistrations.Contains(entity)) continue;

      var newEntity = (IRevisionableEntity)entity.Clone();
      newEntity.Revision = entity.Revision.NewRevision();
      eventData.Context.Add(newEntity);

      entry.Reload();
      entity.Revision.IsCurrent = false;
      
      AdjustNewRevisionReferences(entries, entity, newEntity);
    }

    _updateRegistrations.Clear();
    _referenceUpdateRegistrations.Clear();

    return result;
  }

  protected void AdjustNewRevisionReferences(List<EntityEntry> entries, IRevisionableEntity currentRevision, IRevisionableEntity newRevision)
  {
    foreach (var entry in entries.Where(p => _referenceUpdateRegistrations.Contains(p.Entity)))
    {
      foreach (var property in entry.Entity.GetType().GetProperties())
      {
        if(property.GetValue(entry.Entity) == currentRevision)
          property.SetValue(entry.Entity, newRevision);
      }
    }
  }

  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
  {
    var data = ValueTask.FromResult(SavingChanges(eventData, result));

    return data;
  }
}