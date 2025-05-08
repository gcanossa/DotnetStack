using System;

namespace GKit.DbMigration;

public interface IMigrationMappingsStore : IDisposable
{
  Task LoadAsync();
  Task SaveChangesAsync();

  void Add<S, D>(object sourceKey, object destinationKey) where S : class where D : class;
  bool TryGetSourceKey<S, D>(object destinationKey, out object? sourceKey) where S : class where D : class;
  bool TryGetDestinationKey<S, D>(object sourceKey, out object? destinationKey) where S : class where D : class;
  bool TryGetSourceKey<D>(object destinationKey, out object? sourceKey) where D : class;
  bool TryGetDestinationKey<D>(object sourceKey, out object? destinationKey) where D : class;
}