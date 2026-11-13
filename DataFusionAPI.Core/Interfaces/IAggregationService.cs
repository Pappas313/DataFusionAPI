using DataFusionAPI.Core.Models;

namespace DataFusionAPI.Core.Interfaces;

public interface IAggregationService
{
    /// <param name="category">Optional case-insensitive category filter.</param>
    Task<AggregationResult> AggregateAsync(
        string query,
        SortField sortBy,
        SortDirection sortOrder,
        string? category,
        CancellationToken ct);
}
