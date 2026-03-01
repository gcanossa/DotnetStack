namespace GKit.RENTRI;

public class ApiResultsCache
{
    protected record CacheEntry(DateTimeOffset ExpiresAt, object Value);

    private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    protected Dictionary<string, SemaphoreSlim> _cacheSemaphores = new();
    protected Dictionary<string, CacheEntry> _cache = new();

    protected SemaphoreSlim GetCacheSemaphore(string cacheKey)
    {
        if (!_cacheSemaphores.ContainsKey(cacheKey))
        {
            _semaphore.Wait();
            try
            {
                if (!_cacheSemaphores.ContainsKey(cacheKey))
                {
                    _cacheSemaphores.Add(cacheKey, new SemaphoreSlim(1, 1));
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        return _cacheSemaphores[cacheKey];
    }

    public async ValueTask<T> GetValue<T>(string cacheKey, Func<Task<T>> valueProvider, DateTimeOffset? expiresAt = null)
    {
        if (!_cache.TryGetValue(cacheKey, out var value) || value.ExpiresAt < DateTimeOffset.Now)
        {
            var semaphore = GetCacheSemaphore(cacheKey);
            await semaphore.WaitAsync();
            try
            {
                if(!_cache.TryGetValue(cacheKey, out value) || value.ExpiresAt < DateTimeOffset.Now)
                {
                    value = new CacheEntry(expiresAt ?? DateTimeOffset.Now.AddHours(1), (await valueProvider())!);

                    _cache[cacheKey] = value;
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        return (T)value.Value;
    }

    public void InvalidateKey(string cacheKey)
    {
        _cache.Remove(cacheKey);
    }

    public void InvalidateAll()
    {
        _cache.Clear();
    }
}