using GKit.EntityFramework;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GKit.Tests.Blazor.Infrastructure;

public class Product : ISoftDeletableEntity
{
  public int Id { get; set; }
  public string Name { get; set; } = "";
  public decimal Price { get; set; }
  public DateTime? DeletedAt { get; set; }
}

/// <summary>
/// Mirrors the shape BFer and StuffHR use: a context that installs the soft-delete interceptor
/// and a <c>WithSoftDelete()</c> query filter.
/// </summary>
public class CatalogContext(DbContextOptions options, SoftDeleteInterceptor interceptor)
  : DbContext(options), ISoftDeleteAwareContext
{
  public SoftDeleteInterceptor SoftDeleteInterceptor { get; } = interceptor;

  public DbSet<Product> Products => Set<Product>();

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.AddInterceptors(SoftDeleteInterceptor);

  protected override void OnModelCreating(ModelBuilder builder)
    => builder.Entity<Product>().WithSoftDelete();
}

public sealed class GridFixture : IDisposable
{
  private readonly SqliteConnection _connection;
  private readonly SoftDeleteInterceptor _interceptor = new();

  public GridFixture()
  {
    _connection = new SqliteConnection("DataSource=:memory:");
    _connection.Open();

    using var ctx = NewContext();
    ctx.Database.EnsureCreated();
  }

  public CatalogContext NewContext() =>
    new(new DbContextOptionsBuilder<CatalogContext>().UseSqlite(_connection).Options, _interceptor);

  /// <summary>Seeds <paramref name="live"/> live products and <paramref name="deleted"/> soft-deleted ones.</summary>
  public void Seed(int live, int deleted)
  {
    using var ctx = NewContext();

    for (var i = 0; i < live; i++)
      ctx.Add(new Product { Name = $"live-{i}", Price = i });

    var doomed = new List<Product>();
    for (var i = 0; i < deleted; i++)
    {
      var product = new Product { Name = $"deleted-{i}", Price = 100 + i };
      doomed.Add(product);
      ctx.Add(product);
    }

    ctx.SaveChanges();

    foreach (var product in doomed)
      ctx.Remove(product);

    ctx.SaveChanges();
  }

  public void Dispose() => _connection.Dispose();
}
