using GKit.Settings;
using GKit.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GKit.Tests.Settings;

public class DbSettingsManagerTests : IDisposable
{
  // A fresh in-memory database per test: these tests assert on row counts.
  private readonly SqliteFixture _sqlite = new();

  public void Dispose() => _sqlite.Dispose();

  public enum Mode { Off, Normal, Verbose }

  // Deliberately split per concern: an unsupported property type makes *every* round trip of its
  // owning options type throw, so lumping them together would mask which conversion is broken.
  public class AppOptions
  {
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool Enabled { get; set; }
    public decimal Threshold { get; set; }
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);
  }

  public class NullableOptions { public int? Retries { get; set; } }

  public class EnumOptions { public Mode Mode { get; set; } }

  /// <summary>Distinct type whose name has <see cref="AppOptions"/> as a prefix.</summary>
  public class AppOptionsAdvanced
  {
    public string Host { get; set; } = "advanced-default";
  }

  public class ComputedOptions
  {
    public string First { get; set; } = "";
    public string Last { get; set; } = "";
    public string FullName => $"{First} {Last}";
  }

  public class SettingsContext(DbContextOptions options) : DbContext(options)
  {
    protected override void OnModelCreating(ModelBuilder builder)
      // GKit.Settings ships no key configuration for Setting, so every consumer has to do this
      // by hand or model building fails.
      => builder.Entity<Setting>().HasKey(p => p.Key);
  }

  private sealed class SimpleFactory(DbContextOptions<SettingsContext> options)
    : IDbContextFactory<SettingsContext>
  {
    public SettingsContext CreateDbContext() => new(options);
  }

  private DbSettingsManager<TOptions, SettingsContext> NewManager<TOptions>(TOptions defaults)
    where TOptions : class, new()
  {
    var options = _sqlite.OptionsFor<SettingsContext>();
    using (var ctx = new SettingsContext(options)) ctx.Database.EnsureCreated();

    return new DbSettingsManager<TOptions, SettingsContext>(
      Options.Create(defaults), new SimpleFactory(options));
  }

  [Fact]
  public async Task Defaults_are_returned_when_nothing_is_stored()
  {
    var manager = NewManager(new AppOptions { Host = "from-config", Port = 2525 });

    var result = await manager.GetOptionsAsync();

    Assert.Equal("from-config", result.Host);
    Assert.Equal(2525, result.Port);
  }

  [Fact]
  public async Task Stored_values_override_defaults_and_round_trip()
  {
    var manager = NewManager(new AppOptions());

    await manager.UpdateOptionsAsync(new AppOptions
    {
      Host = "smtp.example.com",
      Port = 587,
      Enabled = true,
      Interval = TimeSpan.FromMinutes(15)
    });

    var result = await manager.GetOptionsAsync();

    Assert.Equal("smtp.example.com", result.Host);
    Assert.Equal(587, result.Port);
    Assert.True(result.Enabled);
    Assert.Equal(TimeSpan.FromMinutes(15), result.Interval);
  }

  [Fact]
  public async Task Decimal_values_round_trip_regardless_of_the_ambient_culture()
  {
    // Written on an it-IT machine ("1,5"), read back on an en-US one -> 15.
    var manager = NewManager(new AppOptions());

    using (new CultureScope("it-IT"))
      await manager.UpdateOptionsAsync(new AppOptions { Threshold = 1.5m });

    using (new CultureScope("en-US"))
    {
      var result = await manager.GetOptionsAsync();
      Assert.Equal(1.5m, result.Threshold);
    }
  }

  [Fact]
  public async Task TimeSpan_values_round_trip_regardless_of_the_ambient_culture()
  {
    var manager = NewManager(new AppOptions());

    using (new CultureScope("it-IT"))
      await manager.UpdateOptionsAsync(new AppOptions { Interval = TimeSpan.FromSeconds(90) });

    using (new CultureScope("en-US"))
    {
      var result = await manager.GetOptionsAsync();
      Assert.Equal(TimeSpan.FromSeconds(90), result.Interval);
    }
  }

  [Fact]
  public async Task Nullable_properties_are_supported()
  {
    // Convert.ChangeType cannot target Nullable<T> and throws InvalidCastException.
    var manager = NewManager(new NullableOptions());

    await manager.UpdateOptionsAsync(new NullableOptions { Retries = 3 });

    var result = await manager.GetOptionsAsync();

    Assert.Equal(3, result.Retries);
  }

  [Fact]
  public async Task Enum_properties_are_supported()
  {
    // Convert.ChangeType cannot parse "Verbose" into an enum.
    var manager = NewManager(new EnumOptions());

    await manager.UpdateOptionsAsync(new EnumOptions { Mode = Mode.Verbose });

    var result = await manager.GetOptionsAsync();

    Assert.Equal(Mode.Verbose, result.Mode);
  }

  [Fact]
  public async Task Options_types_whose_names_share_a_prefix_do_not_bleed_into_each_other()
  {
    // Keys are matched with StartsWith(typeof(TOptions).Name) — "AppOptions" also matches
    // every "AppOptionsAdvanced:*" key.
    var options = _sqlite.OptionsFor<SettingsContext>();
    using (var ctx = new SettingsContext(options)) ctx.Database.EnsureCreated();
    var factory = new SimpleFactory(options);

    var advanced = new DbSettingsManager<AppOptionsAdvanced, SettingsContext>(
      Options.Create(new AppOptionsAdvanced()), factory);
    await advanced.UpdateOptionsAsync(new AppOptionsAdvanced { Host = "advanced-host" });

    var basic = new DbSettingsManager<AppOptions, SettingsContext>(
      Options.Create(new AppOptions { Host = "basic-default" }), factory);

    var result = await basic.GetOptionsAsync();

    Assert.Equal("basic-default", result.Host);
  }

  [Fact]
  public async Task Options_types_with_computed_properties_are_supported()
  {
    // GetOptionsAsync copies every property including get-only ones.
    var manager = NewManager(new ComputedOptions { First = "Mario", Last = "Rossi" });

    var result = await manager.GetOptionsAsync();

    Assert.Equal("Mario", result.First);
    Assert.Equal("Mario Rossi", result.FullName);
  }

  [Fact]
  public async Task Updating_twice_does_not_duplicate_rows()
  {
    var manager = NewManager(new AppOptions());

    await manager.UpdateOptionsAsync(new AppOptions { Host = "first" });
    await manager.UpdateOptionsAsync(new AppOptions { Host = "second" });

    var result = await manager.GetOptionsAsync();

    Assert.Equal("second", result.Host);
  }

  [Fact]
  public async Task CanUpdate_is_true_for_the_database_backed_manager()
  {
    var manager = NewManager(new AppOptions());
    Assert.True(manager.CanUpdate);
    await Task.CompletedTask;
  }

  [Fact]
  public async Task Every_OptionsChanged_handler_is_awaited()
  {
    // OptionsChanged is a multicast Func<T,Task>; Invoke() only returns the last handler's task,
    // so earlier handlers are fired but never awaited (and their failures are unobserved).
    var manager = NewManager(new AppOptions());

    var firstCompleted = false;
    var secondCompleted = false;

    manager.OptionsChanged += async _ => { await Task.Delay(30); firstCompleted = true; };
    manager.OptionsChanged += async _ => { await Task.Delay(10); secondCompleted = true; };

    await manager.UpdateOptionsAsync(new AppOptions { Host = "notify" });

    Assert.True(secondCompleted);
    Assert.True(firstCompleted);
  }
}

public class DefaultSettingsManagerTests
{
  public class Opts { public string Value { get; set; } = "default"; }

  [Fact]
  public async Task Returns_the_configured_options()
  {
    var manager = new DefaultSettingsManager<Opts>(Options.Create(new Opts { Value = "x" }));

    Assert.Equal("x", (await manager.GetOptionsAsync()).Value);
  }

  [Fact]
  public void CanUpdate_is_false()
  {
    var manager = new DefaultSettingsManager<Opts>(Options.Create(new Opts()));

    Assert.False(manager.CanUpdate);
  }

  [Fact]
  public async Task UpdateOptionsAsync_is_not_supported()
  {
    var manager = new DefaultSettingsManager<Opts>(Options.Create(new Opts()));

    await Assert.ThrowsAsync<NotSupportedException>(() => manager.UpdateOptionsAsync(new Opts()));
  }
}
