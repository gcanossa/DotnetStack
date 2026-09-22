using GKit.DbMigration;

namespace GKit.Tests.DbMigration;

public class FileSystemMigrationMappingsStoreTests : IDisposable
{
  private readonly string _path =
    Path.Combine(Path.GetTempPath(), $"gkit-migration-{Guid.NewGuid():N}");

  public class LegacyCustomer { public int Id { get; set; } }
  public class Customer { public int Id { get; set; } }
  public class LegacyOrder { public int Id { get; set; } }
  public class Order { public int Id { get; set; } }

  private FileSystemMigrationMappingsStore NewStore() =>
    new() { MigrationLogsPath = _path };

  public void Dispose()
  {
    if (Directory.Exists(_path)) Directory.Delete(_path, recursive: true);
  }

  [Fact]
  public void A_registered_mapping_is_resolvable_by_source_key()
  {
    using var store = NewStore();

    store.Add<LegacyCustomer, Customer>(1, 100);

    Assert.True(store.TryGetDestinationKey<LegacyCustomer, Customer>(1, out var key));
    Assert.Equal(100, key);
  }

  [Fact]
  public void An_unknown_source_key_is_reported_as_missing()
  {
    using var store = NewStore();

    store.Add<LegacyCustomer, Customer>(1, 100);

    Assert.False(store.TryGetDestinationKey<LegacyCustomer, Customer>(2, out var key));
    Assert.Null(key);
  }

  [Fact]
  public void A_registered_mapping_is_resolvable_by_destination_key()
  {
    // TryGetSourceKey compares boxed object values with ==, i.e. by reference. For int/long/Guid
    // keys the comparison is always false, so the reverse lookup never finds anything.
    using var store = NewStore();

    store.Add<LegacyCustomer, Customer>(1, 100);

    Assert.True(store.TryGetSourceKey<LegacyCustomer, Customer>(100, out var key));
    Assert.Equal(1, key);
  }

  [Fact]
  public void The_single_type_overload_resolves_an_unambiguous_mapping()
  {
    using var store = NewStore();

    store.Add<LegacyCustomer, Customer>(1, 100);

    Assert.True(store.TryGetDestinationKey<Customer>(1, out var key));
    Assert.Equal(100, key);
  }

  [Fact]
  public void The_single_type_overload_returns_false_when_nothing_is_registered()
  {
    using var store = NewStore();

    Assert.False(store.TryGetDestinationKey<Customer>(1, out _));
  }

  [Fact]
  public async Task Mappings_survive_a_save_and_reload_cycle()
  {
    using (var store = NewStore())
    {
      store.Add<LegacyCustomer, Customer>(1, 100);
      store.Add<LegacyOrder, Order>(7, 700);
      await store.SaveChangesAsync();
    }

    using var reloaded = NewStore();
    await reloaded.LoadAsync();

    Assert.True(reloaded.TryGetDestinationKey<LegacyCustomer, Customer>(1, out var customerKey));
    Assert.Equal(100, customerKey);
    Assert.True(reloaded.TryGetDestinationKey<LegacyOrder, Order>(7, out var orderKey));
    Assert.Equal(700, orderKey);
  }

  [Fact]
  public async Task Saving_twice_appends_rather_than_duplicating_the_index()
  {
    using (var store = NewStore())
    {
      store.Add<LegacyCustomer, Customer>(1, 100);
      await store.SaveChangesAsync();
      store.Add<LegacyCustomer, Customer>(2, 200);
      await store.SaveChangesAsync();
    }

    using var reloaded = NewStore();
    await reloaded.LoadAsync();

    Assert.True(reloaded.TryGetDestinationKey<LegacyCustomer, Customer>(1, out _));
    Assert.True(reloaded.TryGetDestinationKey<LegacyCustomer, Customer>(2, out _));
    Assert.Single(await File.ReadAllLinesAsync(Path.Combine(_path, "index.txt")));
  }

  [Fact]
  public async Task Loading_an_existing_directory_without_an_index_does_not_throw()
  {
    // A crash between creating the directory and writing index.txt leaves the migration
    // permanently unresumable: LoadAsync throws FileNotFoundException.
    Directory.CreateDirectory(_path);

    using var store = NewStore();

    var ex = await Record.ExceptionAsync(() => store.LoadAsync());

    Assert.Null(ex);
  }

  [Fact]
  public async Task Loading_a_missing_directory_is_a_no_op()
  {
    using var store = NewStore();

    var ex = await Record.ExceptionAsync(() => store.LoadAsync());

    Assert.Null(ex);
  }

  [Fact]
  public async Task Pending_changes_are_visible_before_they_are_saved()
  {
    // Lookups short-circuit: `!keyMappings.TryGetValue(..) && !keyMappingsChanges.TryGetValue(..)`
    // never consults the pending dictionary once the type pair exists on disk.
    using (var store = NewStore())
    {
      store.Add<LegacyCustomer, Customer>(1, 100);
      await store.SaveChangesAsync();
    }

    using var store2 = NewStore();
    await store2.LoadAsync();
    store2.Add<LegacyCustomer, Customer>(2, 200);

    Assert.True(store2.TryGetDestinationKey<LegacyCustomer, Customer>(2, out var key));
    Assert.Equal(200, key);
  }

  [Fact]
  public void Adding_the_same_source_key_twice_is_reported_clearly()
  {
    using var store = NewStore();

    store.Add<LegacyCustomer, Customer>(1, 100);

    // Dictionary.Add throws a bare ArgumentException naming no entity; a migration re-run
    // should either be idempotent or fail with an actionable message.
    var ex = Record.Exception(() => store.Add<LegacyCustomer, Customer>(1, 101));

    Assert.NotNull(ex);
    Assert.Contains(nameof(Customer), ex.Message);
  }
}
