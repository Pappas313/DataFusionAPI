namespace DataFusionAPI.Core.Models;

/// <summary>
/// Request statistics for a single source. Buckets: fast &lt; 100ms, average 100-200ms, slow &gt; 200ms.
/// </summary>
public record SourceStats(
    int TotalRequests,
    double AverageResponseMs,
    int FastCount,
    int AverageCount,
    int SlowCount);
