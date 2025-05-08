using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GKit.EntityFramework;

public class SoftDeleteInterceptor : SaveChangesInterceptor
{
  protected readonly List<object> _hardDeleteRegistrations = [];

  public void RegisterForHardDelete(object entity)
  {
    if (!_hardDeleteRegistrations.Contains(entity))
      _hardDeleteRegistrations.Add(entity);
  }

  public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
  {
    if (eventData.Context is null) return result;

    foreach (var entry in eventData.Context.ChangeTracker.Entries())
    {
      if (entry is not { State: EntityState.Deleted, Entity: ISoftDeletableEntity entity }
        || _hardDeleteRegistrations.Contains(entity)) continue;
      entry.State = EntityState.Modified;
      entity.DeletedAt = DateTime.Now;
    }
    return result;
  }

  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
  {
    var p = ValueTask.FromResult(SavingChanges(eventData, result));

    _hardDeleteRegistrations.Clear();

    return p;
  }
}
