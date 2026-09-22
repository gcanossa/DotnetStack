using GKit.Authentication.Simple;
using GKit.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GKit.Tests.Authentication;

public class SimpleAccountManagerTests : IDisposable
{
  // Real SQLite rather than the EF in-memory provider: SignInQuery/LookupQuery are relational
  // queries, and IgnoreQueryFilters only means anything against a provider with query filters.
  private readonly SqliteFixture _sqlite = new();

  public void Dispose() => _sqlite.Dispose();

  [Fact]
  public async Task RegistersAndFindsAUser()
  {
    var manager = NewManager();

    Assert.True(await manager.RegisterAsync(new TestUser { AccountName = "user" }, "password"));

    var user = await manager.FindUserAsync("user", "password");

    Assert.NotNull(user);
    Assert.Equal("user", user.AccountName);
  }

  [Fact]
  public async Task RejectsWrongCredentials()
  {
    var manager = NewManager();

    await manager.RegisterAsync(new TestUser { AccountName = "user" }, "password");

    Assert.Null(await manager.FindUserAsync("user", "wrong"));
    Assert.Null(await manager.FindUserAsync("other", "password"));
  }

  [Fact]
  public async Task StoresAHashedPassword()
  {
    var manager = NewManager();

    await manager.RegisterAsync(new TestUser { AccountName = "user" }, "password");

    await using var ctx = NewContext();
    var stored = ctx.Set<TestUser>().Single().PasswordHash;

    Assert.NotNull(stored);
    Assert.DoesNotContain("password", stored);
    Assert.StartsWith("pbkdf2$", stored);
  }

  [Fact]
  public async Task SignInQueryFiltersTheAccountsAllowedToSignIn()
  {
    var manager = NewManager(options => options.SignInQuery = q => q.Where(p => p.IsManager));

    await manager.RegisterAsync(new TestUser { AccountName = "manager", IsManager = true }, "password");
    await manager.RegisterAsync(new TestUser { AccountName = "plain" }, "password");

    Assert.NotNull(await manager.FindUserAsync("manager", "password"));
    Assert.Null(await manager.FindUserAsync("plain", "password"));
  }

  [Fact]
  public async Task ManagementOperationsIgnoreTheSignInQuery()
  {
    var manager = NewManager(options => options.SignInQuery = q => q.Where(p => p.IsManager));

    await manager.RegisterAsync(new TestUser { AccountName = "plain" }, "password");

    // filtered out of sign in, but still manageable
    Assert.True(await manager.ResetPasswordAsync("plain", "other"));
  }

  [Fact]
  public async Task ChangesAPasswordOnlyWithTheCurrentOne()
  {
    var manager = NewManager();

    await manager.RegisterAsync(new TestUser { AccountName = "user" }, "password");

    Assert.False(await manager.ChangePasswordAsync("user", "wrong", "new"));
    Assert.Null(await manager.FindUserAsync("user", "new"));

    Assert.True(await manager.ChangePasswordAsync("user", "password", "new"));
    Assert.NotNull(await manager.FindUserAsync("user", "new"));
    Assert.Null(await manager.FindUserAsync("user", "password"));
  }

  [Fact]
  public async Task ResetsAPasswordWithoutTheCurrentOne()
  {
    var manager = NewManager();

    await manager.RegisterAsync(new TestUser { AccountName = "user" }, "password");

    Assert.True(await manager.ResetPasswordAsync("user", "new"));
    Assert.NotNull(await manager.FindUserAsync("user", "new"));

    Assert.False(await manager.ResetPasswordAsync("missing", "new"));
  }

  [Fact]
  public async Task ReadsBackTheLegacyHashFormat()
  {
    var legacy = new Sha512PasswordHasher();

    await using (var ctx = NewContext())
    {
      ctx.Set<TestUser>().Add(new TestUser { AccountName = "user", PasswordHash = legacy.Hash("password") });
      await ctx.SaveChangesAsync();
    }

    var manager = NewManager(hasher: legacy);

    Assert.NotNull(await manager.FindUserAsync("user", "password"));
    Assert.Null(await manager.FindUserAsync("user", "wrong"));
  }

  private bool _created;

  private AccountContext NewContext()
  {
    var ctx = new AccountContext(_sqlite.OptionsFor<AccountContext>());
    if (!_created)
    {
      ctx.Database.EnsureCreated();
      _created = true;
    }

    return ctx;
  }

  private SimpleAccountManager<AccountContext, TestUser> NewManager(
    Action<SimpleAccountManagerOptions<TestUser>>? configure = null,
    IPasswordHasher? hasher = null)
  {
    var options = new SimpleAccountManagerOptions<TestUser>();
    configure?.Invoke(options);

    return new SimpleAccountManager<AccountContext, TestUser>(
      new HttpContextAccessor(),
      new ContextFactory(this),
      // a low iteration count keeps the suite fast; the cost factor is covered in PasswordHasherTests
      hasher ?? new Pbkdf2PasswordHasher(iterations: 1000),
      Options.Create(options),
      NullLogger<SimpleAccountManager<AccountContext, TestUser>>.Instance);
  }

  public class TestUser : ISimpleAccountUser
  {
    public int Id { get; set; }
    public required string AccountName { get; set; }
    public string? PasswordHash { get; set; }
    public bool IsManager { get; set; }

    public string Identifier => Id.ToString();
  }

  public class AccountContext(DbContextOptions<AccountContext> options) : DbContext(options)
  {
    protected override void OnModelCreating(ModelBuilder builder)
      => builder.Entity<TestUser>().HasKey(p => p.Id);
  }

  private sealed class ContextFactory(SimpleAccountManagerTests owner) : IDbContextFactory<AccountContext>
  {
    public AccountContext CreateDbContext() => owner.NewContext();
  }
}
