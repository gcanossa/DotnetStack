using GKit.EntityFramework;
using GKit.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GKit.Tests.EntityFramework;

public class RevisionTests : IDisposable
{
  // A fresh in-memory database per test: these tests assert on row counts.
  private readonly SqliteFixture _sqlite = new();

  public void Dispose() => _sqlite.Dispose();

  public class Article : IRevisionableEntity
  {
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public decimal Price { get; set; }
    public RevisionInfo Revision { get; set; } = RevisionInfo.First();

    // A revisionable clone must be a *new* row: the key is deliberately not copied.
    public object Clone() => new Article { Title = Title, Price = Price, Revision = Revision.Clone() };
  }

  public class RevisionContext(DbContextOptions options, RevisionInterceptor interceptor)
    : DbContext(options), IRevisionAwareContext
  {
    public RevisionInterceptor RevisionInterceptor { get; } = interceptor;

    public DbSet<Article> Articles => Set<Article>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
      => optionsBuilder.AddInterceptors(RevisionInterceptor);

    protected override void OnModelCreating(ModelBuilder builder)
    {
      builder.Entity<Article>().HasKey(p => p.Id);
      builder.Entity<Article>().WithRevision();
    }
  }

  private RevisionContext NewContext(RevisionInterceptor interceptor, bool create = false)
  {
    var ctx = new RevisionContext(_sqlite.OptionsFor<RevisionContext>(), interceptor);
    if (create) ctx.Database.EnsureCreated();
    return ctx;
  }

  [Fact]
  public async Task Updating_a_revisionable_entity_creates_a_new_current_revision()
  {
    var interceptor = new RevisionInterceptor();
    await using var ctx = NewContext(interceptor, create: true);

    var article = new Article { Title = "v0", Price = 10m };
    ctx.Add(article);
    await ctx.SaveChangesAsync();

    var documentId = article.Revision.DocumentId;

    article.Title = "v1";
    await ctx.SaveChangesAsync();

    await using var verify = NewContext(interceptor);
    var all = await verify.Articles.OrderBy(p => p.Revision.Revision).ToListAsync();

    Assert.Equal(2, all.Count);
    Assert.All(all, a => Assert.Equal(documentId, a.Revision.DocumentId));

    Assert.Equal(0, all[0].Revision.Revision);
    Assert.Equal(1, all[1].Revision.Revision);

    Assert.Single(all.Where(a => a.Revision.IsCurrent));
    Assert.True(all[1].Revision.IsCurrent);
  }

  [Fact]
  public async Task The_superseded_revision_is_persisted_as_not_current()
  {
    // Exactly one row may carry IsCurrent after an update. The interceptor reverts the
    // superseded entry to its original values and then deprecates it.
    var interceptor = new RevisionInterceptor();
    await using var ctx = NewContext(interceptor, create: true);

    var article = new Article { Title = "v0" };
    ctx.Add(article);
    await ctx.SaveChangesAsync();

    article.Title = "v1";
    await ctx.SaveChangesAsync();

    await using var verify = NewContext(interceptor);
    var currentCount = await verify.Articles.CountAsync(p => p.Revision.IsCurrent);

    Assert.Equal(1, currentCount);
  }

  [Fact]
  public async Task Update_with_noRevision_true_updates_in_place()
  {
    var interceptor = new RevisionInterceptor();
    await using var ctx = NewContext(interceptor, create: true);

    var article = new Article { Title = "v0" };
    ctx.Add(article);
    await ctx.SaveChangesAsync();

    article.Title = "corrected typo";
    ctx.Update(article, noRevision: true);
    await ctx.SaveChangesAsync();

    await using var verify = NewContext(interceptor);
    var all = await verify.Articles.ToListAsync();

    Assert.Single(all);
    Assert.Equal("corrected typo", all[0].Title);
    Assert.Equal(0, all[0].Revision.Revision);
  }

  [Fact]
  public async Task Creating_a_revision_issues_no_extra_query()
  {
    // The superseded row used to be recovered with entry.Reload() — a synchronous SELECT per
    // modified entity, on the same connection, while the save was in flight. The original
    // values are already tracked, so the revert needs no round trip at all.
    var counter = new CommandCounter();
    var interceptor = new RevisionInterceptor();

    await using var ctx = new RevisionContext(
      _sqlite.OptionsFor<RevisionContext>(b => b.AddInterceptors(counter)), interceptor);
    await ctx.Database.EnsureCreatedAsync();

    var article = new Article { Title = "v0" };
    ctx.Add(article);
    await ctx.SaveChangesAsync();

    counter.Reset();

    article.Title = "v1";
    await ctx.SaveChangesAsync();

    Assert.Equal(0, counter.Selects);
  }

  [Fact]
  public async Task The_superseded_row_keeps_its_original_values()
  {
    // The edit belongs to the new revision only; reverting the old entry is what makes the
    // history meaningful rather than two rows both showing the latest text.
    var interceptor = new RevisionInterceptor();
    await using var ctx = NewContext(interceptor, create: true);

    var article = new Article { Title = "v0", Price = 10m };
    ctx.Add(article);
    await ctx.SaveChangesAsync();

    article.Title = "v1";
    article.Price = 20m;
    await ctx.SaveChangesAsync();

    await using var verify = NewContext(interceptor);
    var all = await verify.Articles.OrderBy(p => p.Revision.Revision).ToListAsync();

    Assert.Equal("v0", all[0].Title);
    Assert.Equal(10m, all[0].Price);
    Assert.Equal("v1", all[1].Title);
    Assert.Equal(20m, all[1].Price);
  }

  [Fact]
  public void RevisionInfo_First_starts_at_zero_and_is_current()
  {
    var info = RevisionInfo.First();

    Assert.Equal(0, info.Revision);
    Assert.True(info.IsCurrent);
    Assert.False(string.IsNullOrWhiteSpace(info.DocumentId));
  }

  [Fact]
  public void RevisionInfo_NewRevision_increments_and_keeps_the_document_identity()
  {
    var first = RevisionInfo.First();
    var next = first.NewRevision();

    Assert.Equal(first.Revision + 1, next.Revision);
    Assert.Equal(first.DocumentId, next.DocumentId);
    Assert.True(next.IsCurrent);
    Assert.NotSame(first, next);
  }

  [Fact]
  public void RevisionInfo_timestamps_are_UTC()
  {
    var info = RevisionInfo.First();

    Assert.InRange(info.CreatedAt, DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow.AddSeconds(5));
  }

  [Fact]
  public void RevisionInfo_Clone_is_an_independent_copy()
  {
    var original = RevisionInfo.First();
    var clone = original.Clone();

    clone.Deprecate();

    Assert.True(original.IsCurrent);
    Assert.False(clone.IsCurrent);
    Assert.Equal(original.DocumentId, clone.DocumentId);
  }

  [Fact]
  public void DocumentIds_are_unique_per_document()
  {
    var a = RevisionInfo.First();
    var b = RevisionInfo.First();

    Assert.NotEqual(a.DocumentId, b.DocumentId);
  }
}
