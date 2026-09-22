using GKit.EntityFramework;
using GKit.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GKit.Tests.EntityFramework;

public class SoftDeleteTests : IDisposable
{
  // A fresh in-memory database per test: these tests assert on row counts.
  private readonly SqliteFixture _sqlite = new();

  public void Dispose() => _sqlite.Dispose();

  public class Widget : ISoftDeletableEntity
  {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
  }

  public class SoftDeleteContext(DbContextOptions options, SoftDeleteInterceptor interceptor)
    : DbContext(options), ISoftDeleteAwareContext
  {
    public SoftDeleteInterceptor SoftDeleteInterceptor { get; } = interceptor;

    public DbSet<Widget> Widgets => Set<Widget>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
      => optionsBuilder.AddInterceptors(SoftDeleteInterceptor);

    protected override void OnModelCreating(ModelBuilder builder)
      => builder.Entity<Widget>().WithSoftDelete();
  }

  private SoftDeleteContext NewContext(SoftDeleteInterceptor interceptor, bool create = false)
  {
    var ctx = new SoftDeleteContext(_sqlite.OptionsFor<SoftDeleteContext>(), interceptor);
    if (create) ctx.Database.EnsureCreated();
    return ctx;
  }

  [Fact]
  public async Task Remove_soft_deletes_the_row_and_hides_it_from_queries()
  {
    var interceptor = new SoftDeleteInterceptor();
    await using var ctx = NewContext(interceptor, create: true);

    var widget = new Widget { Name = "keep-me" };
    ctx.Add(widget);
    await ctx.SaveChangesAsync();

    ctx.Remove(widget);
    await ctx.SaveChangesAsync();

    Assert.Empty(await ctx.Widgets.ToListAsync());

    var raw = await ctx.Widgets.IgnoreQueryFilters().SingleAsync();
    Assert.NotNull(raw.DeletedAt);
    Assert.Equal("keep-me", raw.Name);
  }

  [Fact]
  public async Task Remove_with_hard_true_really_deletes_the_row()
  {
    var interceptor = new SoftDeleteInterceptor();
    await using var ctx = NewContext(interceptor, create: true);

    var widget = new Widget { Name = "gone" };
    ctx.Add(widget);
    await ctx.SaveChangesAsync();

    ctx.Remove(widget, hard: true);
    await ctx.SaveChangesAsync();

    Assert.Empty(await ctx.Widgets.IgnoreQueryFilters().ToListAsync());
  }

  [Fact]
  public async Task DeletedAt_is_stamped_in_UTC()
  {
    // Audit timestamps must not depend on the server's timezone: a row deleted at 02:30 local
    // during a DST fall-back is otherwise ambiguous, and comparisons across regions break.
    var interceptor = new SoftDeleteInterceptor();
    await using var ctx = NewContext(interceptor, create: true);

    var widget = new Widget { Name = "utc" };
    ctx.Add(widget);
    await ctx.SaveChangesAsync();

    var before = DateTime.UtcNow.AddSeconds(-5);
    ctx.Remove(widget);
    await ctx.SaveChangesAsync();
    var after = DateTime.UtcNow.AddSeconds(5);

    var raw = await ctx.Widgets.IgnoreQueryFilters().SingleAsync();
    Assert.InRange(raw.DeletedAt!.Value, before, after);
  }

  [Fact]
  public async Task Hard_delete_registration_is_not_cleared_by_another_contexts_save()
  {
    // The interceptor keeps hard-delete registrations in a plain List<object> and Clear()s it
    // on *any* SavingChanges. When one interceptor instance is shared (e.g. registered in DI, or
    // reused via a pooled context factory), a concurrent save on another context silently
    // downgrades a hard delete to a soft delete.
    var shared = new SoftDeleteInterceptor();

    await using var seed = NewContext(shared, create: true);
    var a = new Widget { Name = "a" };
    var b = new Widget { Name = "b" };
    seed.AddRange(a, b);
    await seed.SaveChangesAsync();

    await using var ctxA = NewContext(shared);
    await using var ctxB = NewContext(shared);

    var trackedA = await ctxA.Widgets.SingleAsync(p => p.Name == "a");
    var trackedB = await ctxB.Widgets.SingleAsync(p => p.Name == "b");

    ctxA.Remove(trackedA, hard: true);   // registers trackedA for hard delete
    ctxB.Remove(trackedB);               // plain soft delete
    await ctxB.SaveChangesAsync();       // clears the shared registration list
    await ctxA.SaveChangesAsync();

    await using var verify = NewContext(shared);
    var remaining = await verify.Widgets.IgnoreQueryFilters()
      .Select(p => p.Name).OrderBy(p => p).ToListAsync();

    Assert.Equal(["b"], remaining);
  }

  [Fact]
  public async Task Soft_deleting_does_not_stamp_unrelated_entities()
  {
    var interceptor = new SoftDeleteInterceptor();
    await using var ctx = NewContext(interceptor, create: true);

    var target = new Widget { Name = "target" };
    var bystander = new Widget { Name = "bystander" };
    ctx.AddRange(target, bystander);
    await ctx.SaveChangesAsync();

    ctx.Remove(target);
    await ctx.SaveChangesAsync();

    var survivor = await ctx.Widgets.SingleAsync();
    Assert.Equal("bystander", survivor.Name);
    Assert.Null(survivor.DeletedAt);
  }
}
