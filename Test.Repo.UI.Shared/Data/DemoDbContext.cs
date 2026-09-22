using Microsoft.EntityFrameworkCore;

namespace Test.Repo.UI.Shared;

public class DemoDbContext(DbContextOptions<DemoDbContext> options) : DbContext(options)
{
  public DbSet<Widget> Widgets => Set<Widget>();
  public DbSet<Category> Categories => Set<Category>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // A global query filter, so the grid's IgnoreQueryFilters behaviour is actually exercised.
    modelBuilder.Entity<Category>().HasQueryFilter(c => c.Active);
  }
}

/// <summary>
/// Creates and seeds the demo database. Shared by both UI hosts and by the tests.
/// </summary>
public static class DemoData
{
  public static DbContextOptions<DemoDbContext> Options(string databaseName) =>
    new DbContextOptionsBuilder<DemoDbContext>().UseInMemoryDatabase(databaseName).Options;

  public static DemoDbContext Create(string databaseName) => new(Options(databaseName));

  public static DemoDbContext Seed(string databaseName, int widgets = 30)
  {
    var ctx = Create(databaseName);

    if (ctx.Widgets.Any())
      return ctx;

    var categories = new[]
    {
      new Category { Id = 1, Name = "Fasteners" },
      new Category { Id = 2, Name = "Gaskets" },
      new Category { Id = 3, Name = "Retired", Active = false }
    };

    ctx.AddRange(categories);

    ctx.AddRange(Enumerable.Range(1, widgets).Select(i => new Widget
    {
      Id = i,
      Name = $"Widget {i:000}",
      Notes = i % 3 == 0 ? null : $"Note for widget {i}",
      Quantity = (i * 7) % 50,
      CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i),
      // Every third widget has no category, so null-path ordering has something to chew on.
      Category = i % 3 == 0 ? null : categories[i % 2]
    }));

    ctx.SaveChanges();
    ctx.ChangeTracker.Clear();

    return ctx;
  }
}
