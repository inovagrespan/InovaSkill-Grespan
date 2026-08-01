using InovaSkill.Importer.Application.Caching;
using InovaSkill.Importer.Infrastructure.Caching;
using Microsoft.Extensions.Logging.Abstractions;

namespace InovaSkill.Importer.Tests.Caching;

public sealed class ResilientApplicationCacheTests
{
    private static readonly CachePolicy Policy = CachePolicy.For(TimeSpan.FromMinutes(5));

    [Fact]
    public async Task GetOrCreateAsync_MissUsesFallbackAndNextReadUsesCache()
    {
        var store = new FakeCacheStore();
        var cache = CreateCache(store);
        var fallbackCalls = 0;

        Task<string?> Fallback(CancellationToken _)
        {
            fallbackCalls++;
            return Task.FromResult<string?>("resultado do banco");
        }

        Assert.Equal("resultado do banco", await cache.GetOrCreateAsync("key", Fallback, Policy));
        Assert.Equal("resultado do banco", await cache.GetOrCreateAsync("key", Fallback, Policy));
        Assert.Equal(1, fallbackCalls);
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheReadFailureStillReturnsDatabaseFallback()
    {
        var store = new FakeCacheStore { FailReads = true };
        var cache = CreateCache(store);

        var result = await cache.GetOrCreateAsync<string>("key", _ => Task.FromResult<string?>("banco"), Policy);

        Assert.Equal("banco", result);
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheWriteFailureDoesNotFailRequest()
    {
        var store = new FakeCacheStore { FailWrites = true };
        var cache = CreateCache(store);

        var result = await cache.GetOrCreateAsync<string>("key", _ => Task.FromResult<string?>("banco"), Policy);

        Assert.Equal("banco", result);
    }

    [Fact]
    public async Task GetOrCreateAsync_ConcurrentMissesExecuteFallbackOnce()
    {
        var cache = CreateCache(new FakeCacheStore());
        var fallbackCalls = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<string?> Fallback(CancellationToken _)
        {
            Interlocked.Increment(ref fallbackCalls);
            await release.Task;
            return "banco";
        }

        var requests = Enumerable.Range(0, 10).Select(_ => cache.GetOrCreateAsync("shared", Fallback, Policy)).ToArray();
        await Task.Delay(20);
        release.SetResult();

        Assert.All(await Task.WhenAll(requests), result => Assert.Equal("banco", result));
        Assert.Equal(1, fallbackCalls);
    }

    [Fact]
    public async Task GetOrCreateAsync_DoesNotCacheNullByDefault()
    {
        var cache = CreateCache(new FakeCacheStore());
        var fallbackCalls = 0;
        Task<string?> Fallback(CancellationToken _) { fallbackCalls++; return Task.FromResult<string?>(null); }

        await cache.GetOrCreateAsync("key", Fallback, Policy);
        await cache.GetOrCreateAsync("key", Fallback, Policy);

        Assert.Equal(2, fallbackCalls);
    }

    [Fact]
    public void CacheKey_IsVersionedDeterministicAndBoundsLongValues()
    {
        Assert.Equal("inovaskill:products:v1:page:1", CacheKey.Create("Products", "V1", "Page", 1));
        var longKey = CacheKey.Create("products", "v1", new string('a', 500));
        Assert.StartsWith("inovaskill:products:v1:sha256:", longKey);
        Assert.True(longKey.Length < 200);
    }

    private static ResilientApplicationCache CreateCache(ICacheStore store) =>
        new(store, NullLogger<ResilientApplicationCache>.Instance);

    private sealed class FakeCacheStore : ICacheStore
    {
        private readonly Dictionary<string, byte[]> values = [];
        public bool FailReads { get; init; }
        public bool FailWrites { get; init; }

        public Task<CacheStoreEntry> GetAsync(string key, CancellationToken cancellationToken)
        {
            if (FailReads) throw new InvalidOperationException("cache indisponível");
            return Task.FromResult(values.TryGetValue(key, out var value) ? CacheStoreEntry.Hit(value) : CacheStoreEntry.Miss);
        }

        public Task SetAsync(string key, byte[] value, TimeSpan absoluteExpiration, CancellationToken cancellationToken)
        {
            if (FailWrites) throw new InvalidOperationException("cache indisponível");
            values[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            values.Remove(key);
            return Task.CompletedTask;
        }
    }
}
