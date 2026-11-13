using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Core.Models;
using Microsoft.Extensions.Logging;

namespace DataFusionAPI.Infrastructure.Aggregation;

public class AggregationService : IAggregationService
{
    private readonly IEnumerable<IAggregationSource> _clients;
    private readonly ILogger<AggregationService> _logger;

    public AggregationService(IEnumerable<IAggregationSource> clients, ILogger<AggregationService> logger)
    {
        _clients = clients;
        _logger = logger;
    }

    public async Task<AggregationResult> AggregateAsync(
        string query,
        SortField sortBy,
        SortDirection sortOrder,
        string? category,
        CancellationToken ct)
    {
        var outcomes = await Task.WhenAll(_clients.Select(client => FetchSafelyAsync(client, query, ct)));

        var failedSources = outcomes
            .Where(o => o.Result is null)
            .Select(o => o.Source)
            .ToList();

        var staleSources = outcomes
            .Where(o => o.Result is { IsStale: true })
            .Select(o => o.Source)
            .ToList();

        IEnumerable<AggregatedItem> items = outcomes
            .Where(o => o.Result is not null)
            .SelectMany(o => ScoreByRank(o.Result!.Items));

        if (!string.IsNullOrWhiteSpace(category))
        {
            items = items.Where(i => string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        return new AggregationResult
        {
            Items = Sort(items, sortBy, sortOrder).ToList(),
            FailedSources = failedSources,
            StaleSources = staleSources
        };
    }

    private async Task<(string Source, SourceResult? Result)> FetchSafelyAsync(
        IAggregationSource client, string query, CancellationToken ct)
    {
        try
        {
            return (client.SourceName, await client.FetchAsync(query, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "External source {Source} failed.", client.SourceName);
            return (client.SourceName, (SourceResult?)null);
        }
    }

    // Each API returns results best match first, so position within the source is a relevance
    // signal that is comparable across sources (raw scores are on unrelated scales).
    private static IEnumerable<AggregatedItem> ScoreByRank(IReadOnlyList<AggregatedItem> items) =>
        items.Select((item, index) => item with { Score = 1.0 - (double)index / items.Count });

    private static IEnumerable<AggregatedItem> Sort(
        IEnumerable<AggregatedItem> items, SortField sortBy, SortDirection sortOrder)
    {
        var ascending = sortOrder == SortDirection.Asc;

        if (sortBy == SortField.Date)
        {
            // Undated items always go last, in both directions.
            var datedFirst = items.OrderBy(i => i.PublishedOrRelevantDate is null);
            return ascending
                ? datedFirst.ThenBy(i => i.PublishedOrRelevantDate)
                : datedFirst.ThenByDescending(i => i.PublishedOrRelevantDate);
        }

        return ascending ? items.OrderBy(i => i.Score) : items.OrderByDescending(i => i.Score);
    }
}
