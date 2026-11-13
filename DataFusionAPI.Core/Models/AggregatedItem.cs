namespace DataFusionAPI.Core.Models;

public record AggregatedItem(
    string Id,
    string Source,
    string Title,
    string Description,
    string Url,
    DateTimeOffset? PublishedOrRelevantDate,
    string Category,
    double RawScore)
{
    /// <summary>Relevance from 0 to 1, comparable across sources.</summary>
    public double Score { get; init; }
}
