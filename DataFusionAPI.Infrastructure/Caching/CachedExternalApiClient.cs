using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Core.Models;
using DataFusionAPI.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DataFusionAPI.Infrastructure.Caching;

/// <summary>
/// Decorator that caches results of an inner <see cref="IExternalApiClient"/> and
/// falls back to the last known value (flagged stale) when the inner client fails.
/// Concurrent requests for the same key share a single upstream call.
/// </summary>
public class CachedExternalApiClient : IAggregationSource
{
    // Striped locks: bounded memory regardless of how many distinct queries are seen.
    private static readonly SemaphoreSlim[] Locks =
        Enumerable.Range(0, 256).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private readonly IExternalApiClient _inner;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _freshEntryOptions;
    private readonly MemoryCacheEntryOptions _fallbackEntryOptions;

    public CachedExternalApiClient(IExternalApiClient inner, IMemoryCache cache, IOptions<CacheOptions> options)
    {
        _inner = inner;
        _cache = cache;

        // Size = 1 lets MemoryCacheOptions.SizeLimit (CacheOptions.MaxEntries) bound the cache.
        _freshEntryOptions = new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(options.Value.FreshSeconds)
        };
        _fallbackEntryOptions = new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(options.Value.FallbackHours)
        };
    }

    public string SourceName => _inner.SourceName;

    public async Task<SourceResult> FetchAsync(string query, CancellationToken cancellationToken)
    {
        // One cached result stands for every spelling of the query, so send the normalised form upstream too.
        query = query.Trim().ToLowerInvariant();
        var key = $"{SourceName}:{query}";

        if (TryGetFresh(key, out var cached))
        {
            return new SourceResult(cached, IsStale: false);
        }

        var gate = Locks[(uint)key.GetHashCode() % Locks.Length];
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have populated the cache while we waited.
            if (TryGetFresh(key, out cached))
            {
                return new SourceResult(cached, IsStale: false);
            }

            try
            {
                var items = await _inner.FetchAsync(query, cancellationToken);

                _cache.Set(key, items, _freshEntryOptions);
                _cache.Set(FallbackKey(key), items, _fallbackEntryOptions);

                return new SourceResult(items, IsStale: false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                if (_cache.TryGetValue(FallbackKey(key), out IReadOnlyList<AggregatedItem>? stale) && stale is not null)
                {
                    return new SourceResult(stale, IsStale: true);
                }

                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static string FallbackKey(string key) => $"lkg:{key}";

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
