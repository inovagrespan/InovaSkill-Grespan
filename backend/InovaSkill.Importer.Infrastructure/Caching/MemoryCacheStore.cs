using InovaSkill.Importer.Application.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace InovaSkill.Importer.Infrastructure.Caching;

public sealed class MemoryCacheStore(IMemoryCache memoryCache) : ICacheStore
{
    public Task<CacheStoreEntry> GetAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(memoryCache.TryGetValue<byte[]>(key, out var value) && value is not null
            ? CacheStoreEntry.Hit(value)
            : CacheStoreEntry.Miss);
    }

    public Task SetAsync(string key, byte[] value, TimeSpan absoluteExpiration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        memoryCache.Set(key, value, absoluteExpiration);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        memoryCache.Remove(key);
        return Task.CompletedTask;
    }
}
