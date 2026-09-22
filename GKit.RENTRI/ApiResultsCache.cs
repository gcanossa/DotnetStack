using System.Collections.Concurrent;

namespace GKit.RENTRI;

/// <summary>
/// Caches RENTRI lookup results.
/// <para>
/// Reached concurrently by construction — Blazor circuits share one instance — so the backing
/// stores are concurrent. Plain <see cref="Dictionary{TKey,TValue}"/> instances were being read
/// on the fast path while another thread wrote, which can corrupt the bucket chain and spin
/// forever inside a lookup.
/// </para>
/// </summary>
public class ApiResultsCache
{
    protected record CacheEntry(DateTimeOffset ExpiresAt, object Value);

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _cacheSemaphores = new();
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    protected SemaphoreSlim GetCacheSemaphore(string cacheKey) =>
        _cacheSemaphores.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

    private static bool IsFresh(CacheEntry entry) => entry.ExpiresAt > DateTimeOffset.UtcNow;

    public async ValueTask<T> GetValue<T>(string cacheKey, Func<Task<T>> valueProvider, DateTimeOffset? expiresAt = null)
    {
        if (_cache.TryGetValue(cacheKey, out var cached) && IsFresh(cached))
            return (T)cached.Value;

        var semaphore = GetCacheSemaphore(cacheKey);
        await semaphore.WaitAsync();
        try
        {
            // Re-check: another caller may have populated it while this one queued.
            if (_cache.TryGetValue(cacheKey, out cached) && IsFresh(cached))
                return (T)cached.Value;

            var entry = new CacheEntry(expiresAt ?? DateTimeOffset.UtcNow.AddHours(1), (await valueProvider())!);
            _cache[cacheKey] = entry;

            return (T)entry.Value;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void InvalidateKey(string cacheKey) => _cache.TryRemove(cacheKey, out _);

    public void InvalidateAll() => _cache.Clear();
}
