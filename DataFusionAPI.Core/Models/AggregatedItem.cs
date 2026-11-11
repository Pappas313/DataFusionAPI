namespace DataFusionAPI.Core.Models;

public record AggregatedItem(
    string Id,
    string Source,
    string Title,
    string Description,
    string Url,
    DateTimeOffset PublishedOrRelevantDate,
    string Category,
    double RawScore);
