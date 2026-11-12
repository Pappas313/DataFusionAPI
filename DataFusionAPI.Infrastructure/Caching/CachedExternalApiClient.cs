using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace DataFusionAPI.Infrastructure.Caching;

/// <summary>
/// Decorator that caches results of an inner <see cref="IExternalApiClient"/> and
/// falls back to the last known value when the inner client fails.
/// Concurrent requests for the same key share a single upstream call.
/// </summary>
public class CachedExternalApiClient : IExternalApiClient
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LastKnownGoodDuration = TimeSpan.FromHours(24);

    // Striped locks: bounded memory regardless of how many distinct queries are seen.
    private static readonly SemaphoreSlim[] Locks =
        Enumerable.Range(0, 256).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private readonly IExternalApiClient _inner;
    private readonly IMemoryCache _cache;

    public CachedExternalApiClient(IExternalApiClient inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public string SourceName => _inner.SourceName;

    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(string query, CancellationToken cancellationToken)
    {
        var key = $"{SourceName}:{query}";

        if (TryGetFresh(key, out var cached))
        {
            return cached;
        }

        var gate = Locks[(uint)key.GetHashCode() % Locks.Length];
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have populated the cache while we waited.
            if (TryGetFresh(key, out cached))
            {
                return cached;
            }

            try
            {
                var items = await _inner.FetchAsync(query, cancellationToken);

                _cache.Set(key, items, CacheDuration);
                _cache.Set(LastKnownGoodKey(key), items, LastKnownGoodDuration);

                return items;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                if (_cache.TryGetValue(LastKnownGoodKey(key), out IReadOnlyList<AggregatedItem>? stale) && stale is not null)
                {
                    return stale;
                }

                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static string LastKnownGoodKey(string key) => $"lkg:{key}";

    private bool TryGetFresh(string key, out IReadOnlyList<AggregatedItem> items)
    {
        if (_cache.TryGetValue(key, out IReadOnlyList<AggregatedItem>? cached) && cached is not null)
        {
            items = cached;
            return true;
        }

        items = [];
        return false;
    }
}
