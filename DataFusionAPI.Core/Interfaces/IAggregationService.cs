using DataFusionAPI.Core.Models;

namespace DataFusionAPI.Core.Interfaces;

public interface IAggregationService
{
    /// <param name="sortBy">"date" or "relevance" (default).</param>
    /// <param name="sortOrder">"asc" or "desc" (default).</param>
    /// <param name="category">Optional case-insensitive category filter.</param>
    Task<AggregationResult> AggregateAsync(
        string query,
        string? sortBy,
        string? sortOrder,
        string? category,
        CancellationToken ct);
}
