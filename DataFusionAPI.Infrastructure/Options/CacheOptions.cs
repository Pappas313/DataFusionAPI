using System.ComponentModel.DataAnnotations;

namespace DataFusionAPI.Infrastructure.Options;

public sealed class CacheOptions
{
    public const string Section = "Aggregation:Cache";

    /// <summary>How long a result is served from cache without calling the source.</summary>
    [Range(1, 86_400)]
    public int FreshSeconds { get; init; } = 60;

    /// <summary>How long the last successful result is kept as a fallback when a source fails.</summary>
    [Range(1, 8_760)]
    public int FallbackHours { get; init; } = 24;

    /// <summary>Maximum number of cache entries. Each query uses two (fresh and fallback).</summary>
    [Range(2, 1_000_000)]
    public int MaxEntries { get; init; } = 10_000;
}
