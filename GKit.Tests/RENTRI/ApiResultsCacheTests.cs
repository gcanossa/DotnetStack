using GKit.RENTRI;

namespace GKit.Tests.RENTRI;

public class ApiResultsCacheTests
{
  [Fact]
  public async Task A_value_is_computed_once_and_then_served_from_the_cache()
  {
    var cache = new ApiResultsCache();
    var calls = 0;

    for (var i = 0; i < 5; i++)
      await cache.GetValue("k", () => { Interlocked.Increment(ref calls); return Task.FromResult("v"); });

    Assert.Equal(1, calls);
  }

  [Fact]
  public async Task Concurrent_callers_compute_the_value_only_once()
  {
    // The cache is shared across Blazor circuits, so this is the normal access pattern rather
    // than an edge case. The previous plain-Dictionary implementation could also corrupt its
    // bucket chain here and spin forever inside a lookup.
    var cache = new ApiResultsCache();
    var calls = 0;

    var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(async () =>
      await cache.GetValue("shared", async () =>
      {
        Interlocked.Increment(ref calls);
        await Task.Delay(20);
        return "v";
      })));

    var results = await Task.WhenAll(tasks);

    Assert.Equal(1, calls);
    Assert.All(results, r => Assert.Equal("v", r));
  }

  [Fact]
  public async Task Many_keys_can_be_populated_concurrently_without_corruption()
  {
    var cache = new ApiResultsCache();

    var tasks = Enumerable.Range(0, 200).Select(i => Task.Run(async () =>
      await cache.GetValue($"key-{i}", () => Task.FromResult(i))));

    var results = await Task.WhenAll(tasks);

    Assert.Equal(Enumerable.Range(0, 200), results.OrderBy(x => x));
  }

  [Fact]
  public async Task An_expired_entry_is_recomputed()
  {
    var cache = new ApiResultsCache();
    var calls = 0;

    Task<string> Provider() { calls++; return Task.FromResult($"v{calls}"); }

    await cache.GetValue("k", Provider, DateTimeOffset.UtcNow.AddMilliseconds(-1));
    var second = await cache.GetValue("k", Provider, DateTimeOffset.UtcNow.AddMinutes(5));

    Assert.Equal(2, calls);
    Assert.Equal("v2", second);
  }

  [Fact]
  public async Task Expiry_is_measured_in_UTC()
  {
    // DateTimeOffset.Now vs UtcNow both carry an offset, but mixing them with a UtcNow-based
    // expiry makes freshness depend on the server's timezone.
    var cache = new ApiResultsCache();
    var calls = 0;

    for (var i = 0; i < 3; i++)
      await cache.GetValue("k", () => { calls++; return Task.FromResult("v"); },
        DateTimeOffset.UtcNow.AddMinutes(5));

    Assert.Equal(1, calls);
  }

  [Fact]
  public async Task InvalidateKey_forces_a_recompute_of_only_that_key()
  {
    var cache = new ApiResultsCache();
    var a = 0;
    var b = 0;

    await cache.GetValue("a", () => { a++; return Task.FromResult("a"); });
    await cache.GetValue("b", () => { b++; return Task.FromResult("b"); });

    cache.InvalidateKey("a");

    await cache.GetValue("a", () => { a++; return Task.FromResult("a"); });
    await cache.GetValue("b", () => { b++; return Task.FromResult("b"); });

    Assert.Equal(2, a);
    Assert.Equal(1, b);
  }

  [Fact]
  public async Task InvalidateAll_clears_every_key()
  {
    var cache = new ApiResultsCache();
    var calls = 0;

    await cache.GetValue("a", () => { calls++; return Task.FromResult("a"); });
    cache.InvalidateAll();
    await cache.GetValue("a", () => { calls++; return Task.FromResult("a"); });

    Assert.Equal(2, calls);
  }
}
