using DataFusionAPI.Core.Models;

namespace DataFusionAPI.Core.Interfaces;

/// <summary>What the aggregation service depends on. Raw API clients keep <see cref="IExternalApiClient"/>.</summary>
public interface IAggregationSource
{
    string SourceName { get; }

    Task<SourceResult> FetchAsync(string query, CancellationToken cancellationToken);
}
