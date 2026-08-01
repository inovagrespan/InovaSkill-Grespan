namespace InovaSkill.Importer.Application.Caching;

public interface IApplicationCache
{
    Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> databaseFallback,
        CachePolicy policy,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}

public sealed record CachePolicy(TimeSpan AbsoluteExpiration, bool CacheNullValues = false)
{
    public static CachePolicy For(TimeSpan absoluteExpiration, bool cacheNullValues = false)
    {
        if (absoluteExpiration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(absoluteExpiration), "A expiração do cache deve ser positiva.");

        return new CachePolicy(absoluteExpiration, cacheNullValues);
    }
}

public interface ICacheStore
{
    Task<CacheStoreEntry> GetAsync(string key, CancellationToken cancellationToken);
    Task SetAsync(string key, byte[] value, TimeSpan absoluteExpiration, CancellationToken cancellationToken);
    Task RemoveAsync(string key, CancellationToken cancellationToken);
}

public readonly record struct CacheStoreEntry(bool Found, byte[]? Value)
{
    public static CacheStoreEntry Miss => new(false, null);
    public static CacheStoreEntry Hit(byte[] value) => new(true, value);
}
