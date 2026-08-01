using System.Collections.Concurrent;
using System.Text.Json;
using InovaSkill.Importer.Application.Caching;
using Microsoft.Extensions.Logging;

namespace InovaSkill.Importer.Infrastructure.Caching;

public sealed class ResilientApplicationCache(
    ICacheStore store,
    ILogger<ResilientApplicationCache> logger) : IApplicationCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, Lazy<Task<object?>>> inFlightFallbacks = new(StringComparer.Ordinal);

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> databaseFallback,
        CachePolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(databaseFallback);
        ValidatePolicy(policy);

        var cached = await TryReadAsync<T>(key, cancellationToken);
        if (cached.Found) return cached.Value;

        var operation = inFlightFallbacks.GetOrAdd(key, _ => new Lazy<Task<object?>>(
            async () => await ResolveMissAsync(key, databaseFallback, policy, CancellationToken.None),
            LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return (T?)await operation.Value.WaitAsync(cancellationToken);
        }
        finally
        {
            if (operation.IsValueCreated && operation.Value.IsCompleted)
                inFlightFallbacks.TryRemove(new KeyValuePair<string, Lazy<Task<object?>>>(key, operation));
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        try { await store.RemoveAsync(key, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { logger.LogWarning(exception, "Falha não bloqueante ao remover a chave de cache {CacheKey}.", key); }
    }

    private async Task<object?> ResolveMissAsync<T>(
        string key, Func<CancellationToken, Task<T?>> databaseFallback, CachePolicy policy, CancellationToken cancellationToken)
    {
        var cachedAfterLock = await TryReadAsync<T>(key, cancellationToken);
        if (cachedAfterLock.Found) return cachedAfterLock.Value;

        var value = await databaseFallback(cancellationToken);
        if (value is not null || policy.CacheNullValues) await TryWriteAsync(key, value, policy, cancellationToken);
        return value;
    }

    private async Task<(bool Found, T? Value)> TryReadAsync<T>(string key, CancellationToken cancellationToken)
    {
        try
        {
            var entry = await store.GetAsync(key, cancellationToken);
            if (!entry.Found || entry.Value is null) return (false, default);
            var envelope = JsonSerializer.Deserialize<CacheEnvelope<T>>(entry.Value, SerializerOptions);
            if (envelope is null) return (false, default);
            logger.LogDebug("Cache hit para {CacheKey}.", key);
            return (true, envelope.Value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Falha não bloqueante ao ler a chave de cache {CacheKey}; o fallback será utilizado.", key);
            return (false, default);
        }
    }

    private async Task TryWriteAsync<T>(string key, T? value, CachePolicy policy, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new CacheEnvelope<T>(value), SerializerOptions);
            await store.SetAsync(key, bytes, policy.AbsoluteExpiration, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { logger.LogWarning(exception, "Falha não bloqueante ao preencher a chave de cache {CacheKey}.", key); }
    }

    private static void ValidatePolicy(CachePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.AbsoluteExpiration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(policy), "A expiração do cache deve ser positiva.");
    }

    private sealed record CacheEnvelope<T>(T? Value);
}
