namespace DataFusionAPI.Core.Models;

/// <summary>Items from one source, and whether they are a last-known-good copy served after a failure.</summary>
public record SourceResult(IReadOnlyList<AggregatedItem> Items, bool IsStale);
