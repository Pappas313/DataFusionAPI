using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace DataFusionAPI.Infrastructure.Caching;

/// <summary>
/// Decorator that caches results of an inner <see cref="IExternalApiClient"/> and
/// falls back to the last known value when the inner client fails.
/// </summary>
public class CachedExternalApiClient : IExternalApiClient
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LastKnownGoodDuration = TimeSpan.FromHours(24);

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
        var lastKnownGoodKey = $"lkg:{key}";

        if (_cache.TryGetValue(key, out IReadOnlyList<AggregatedItem>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var items = await _inner.FetchAsync(query, cancellationToken);

            _cache.Set(key, items, CacheDuration);
            _cache.Set(lastKnownGoodKey, items, LastKnownGoodDuration);

            return items;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (_cache.TryGetValue(lastKnownGoodKey, out IReadOnlyList<AggregatedItem>? stale) && stale is not null)
            {
                return stale;
            }

            throw;
        }
    }
}
