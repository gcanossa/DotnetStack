using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GKit.EntityFramework;

public class RevisionInterceptor : SaveChangesInterceptor
{
  private sealed class Registrations
  {
    public HashSet<object> SuppressRevision { get; } = new(ReferenceEqualityComparer.Instance);
    public HashSet<object> UpdateReferences { get; } = new(ReferenceEqualityComparer.Instance);
  }

  /// <summary>
  /// Registrations scoped to the <see cref="DbContext"/> that made them. A single interceptor
  /// instance is shared by every context built from the same <see cref="DbContextOptions"/>,
  /// so flat lists would let one context's save clear another context's registrations.
  /// </summary>
  private readonly ConditionalWeakTable<DbContext, Registrations> _registrations = [];

  private Registrations GetOrAdd(DbContext context)
  {
    lock (_registrations)
    {
      return _registrations.GetValue(context, _ => new Registrations());
    }
  }

  private Registrations Take(DbContext context)
  {
    lock (_registrations)
    {
      if (!_registrations.TryGetValue(context, out var registrations))
        return new Registrations();

      _registrations.Remove(context);
      return registrations;
    }
  }

  public void RegisterForUpdate(DbContext context, IRevisionableEntity entity)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(entity);

    GetOrAdd(context).SuppressRevision.Add(entity);
  }

  public void RegisterForRevisionReferenceUpdate(DbContext context, object entity)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(entity);

    GetOrAdd(context).UpdateReferences.Add(entity);
  }

  public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
  {
    if (eventData.Context is null) return result;

    ApplyRevisions(eventData.Context);

    return result;
  }

  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
  {
    if (eventData.Context is null) return ValueTask.FromResult(result);

    ApplyRevisions(eventData.Context);

    return ValueTask.FromResult(result);
  }

  private void ApplyRevisions(DbContext context)
  {
    var pending = CollectPendingRevisions(context);

    foreach (var (entry, current, _) in pending)
    {
      // The superseded row must keep the values it had in the database — the edit belongs to
      // the new revision only.
      //
      // This used to call entry.Reload(), a synchronous SELECT per modified entity issued on
      // the same connection while the save was in flight: on SQL Server without MARS that
      // throws, and inside an explicit transaction it can deadlock. The original values are
      // already tracked, so no query is needed to recover them.
      entry.CurrentValues.SetValues(entry.OriginalValues);

      // Then deprecate. Done after the revert so it is the one surviving modification.
      current.Revision.IsCurrent = false;
    }
  }

  private List<(EntityEntry Entry, IRevisionableEntity Current, IRevisionableEntity Next)>
    CollectPendingRevisions(DbContext context)
  {
    var registrations = Take(context);

    var entries = context.ChangeTracker.Entries().ToList();
    var pending = new List<(EntityEntry, IRevisionableEntity, IRevisionableEntity)>();

    foreach (var entry in entries)
    {
      if (entry is not { State: EntityState.Modified, Entity: IRevisionableEntity entity }
          || registrations.SuppressRevision.Contains(entity)) continue;

      var newEntity = (IRevisionableEntity)entity.Clone();
      newEntity.Revision = entity.Revision.NewRevision();
      context.Add(newEntity);

      AdjustNewRevisionReferences(entries, registrations, entity, newEntity);

      pending.Add((entry, entity, newEntity));
    }

    return pending;
  }

  private static void AdjustNewRevisionReferences(
    List<EntityEntry> entries,
    Registrations registrations,
    IRevisionableEntity currentRevision,
    IRevisionableEntity newRevision)
  {
    foreach (var entry in entries.Where(p => registrations.UpdateReferences.Contains(p.Entity)))
    {
      // Indexers would throw TargetParameterCountException on GetValue.
      var properties = entry.Entity.GetType().GetProperties()
        .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);

      foreach (var property in properties)
      {
        if (ReferenceEquals(property.GetValue(entry.Entity), currentRevision))
          property.SetValue(entry.Entity, newRevision);
      }
    }
  }
}
