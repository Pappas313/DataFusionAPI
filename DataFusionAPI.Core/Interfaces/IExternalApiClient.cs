using DataFusionAPI.Core.Models;

namespace DataFusionAPI.Core.Interfaces;

public interface IExternalApiClient
{
    string SourceName { get; }

    Task<IReadOnlyList<AggregatedItem>> FetchAsync(string query, CancellationToken cancellationToken);
}
