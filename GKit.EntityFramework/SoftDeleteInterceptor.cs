using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GKit.EntityFramework;

public class SoftDeleteInterceptor : SaveChangesInterceptor
{
  /// <summary>
  /// Hard-delete opt-outs, scoped to the <see cref="DbContext"/> that registered them.
  /// A single interceptor instance is shared by every context built from the same
  /// <see cref="DbContextOptions"/>, so a flat list would let one context's save clear — or
  /// consume — another context's registrations.
  /// </summary>
  private readonly ConditionalWeakTable<DbContext, HashSet<object>> _hardDeleteRegistrations = [];

  public void RegisterForHardDelete(DbContext context, object entity)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(entity);

    lock (_hardDeleteRegistrations)
    {
      _hardDeleteRegistrations.GetValue(context, _ => new HashSet<object>(ReferenceEqualityComparer.Instance))
        .Add(entity);
    }
  }

  private HashSet<object> TakeRegistrations(DbContext context)
  {
    lock (_hardDeleteRegistrations)
    {
      if (!_hardDeleteRegistrations.TryGetValue(context, out var registrations))
        return [];

      _hardDeleteRegistrations.Remove(context);
      return registrations;
    }
  }

  public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
  {
    ApplySoftDelete(eventData.Context);

    return result;
  }

  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
  {
    ApplySoftDelete(eventData.Context);

    return ValueTask.FromResult(result);
  }

  private void ApplySoftDelete(DbContext? context)
  {
    if (context is null) return;

    var hardDeletes = TakeRegistrations(context);

    foreach (var entry in context.ChangeTracker.Entries())
    {
      if (entry is not { State: EntityState.Deleted, Entity: ISoftDeletableEntity entity }
          || hardDeletes.Contains(entity)) continue;

      entry.State = EntityState.Modified;
      entity.DeletedAt = DateTime.UtcNow;
    }
  }
}
