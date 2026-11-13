namespace DataFusionAPI.Core.Models;

public class AggregationResult
{
    public IReadOnlyList<AggregatedItem> Items { get; init; } = [];

    public IReadOnlyList<string> FailedSources { get; init; } = [];

    public IReadOnlyList<string> StaleSources { get; init; } = [];
}
