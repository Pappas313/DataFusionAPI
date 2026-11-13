using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Core.Models;
using Microsoft.Extensions.Logging;

namespace DataFusionAPI.Infrastructure.Aggregation;

public class AggregationService : IAggregationService
{
    private readonly IEnumerable<IExternalApiClient> _clients;
    private readonly ILogger<AggregationService> _logger;

    public AggregationService(IEnumerable<IExternalApiClient> clients, ILogger<AggregationService> logger)
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
            .Where(o => o.Items is null)
            .Select(o => o.Source)
            .ToList();

        IEnumerable<AggregatedItem> items = outcomes.Where(o => o.Items is not null).SelectMany(o => o.Items!);

        if (!string.IsNullOrWhiteSpace(category))
        {
            items = items.Where(i => string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        return new AggregationResult
        {
            Items = Sort(items, sortBy, sortOrder).ToList(),
            FailedSources = failedSources
        };
    }

    private async Task<(string Source, IReadOnlyList<AggregatedItem>? Items)> FetchSafelyAsync(
        IExternalApiClient client, string query, CancellationToken ct)
    {
        try
        {
            return (client.SourceName, await client.FetchAsync(query, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "External source {Source} failed.", client.SourceName);
            return (client.SourceName, null);
        }
    }

    private static IEnumerable<AggregatedItem> Sort(
        IEnumerable<AggregatedItem> items, SortField sortBy, SortDirection sortOrder)
    {
        var ascending = sortOrder == SortDirection.Asc;
        var byDate = sortBy == SortField.Date;

        return (byDate, ascending) switch
        {
            (true, true) => items.OrderBy(i => i.PublishedOrRelevantDate),
            (true, false) => items.OrderByDescending(i => i.PublishedOrRelevantDate),
            (false, true) => items.OrderBy(i => i.RawScore),
            (false, false) => items.OrderByDescending(i => i.RawScore)
        };
    }
}
