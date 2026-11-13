using System.ComponentModel.DataAnnotations;

namespace DataFusionAPI.Infrastructure.Options;

public sealed class StatsOptions : IValidatableObject
{
    public const string Section = "Aggregation:Stats";

    /// <summary>Responses faster than this many milliseconds are "fast".</summary>
    [Range(1, 600_000)]
    public int FastBelowMs { get; init; } = 100;

    /// <summary>Responses slower than this many milliseconds are "slow"; in between is "average".</summary>
    [Range(1, 600_000)]
    public int SlowAboveMs { get; init; } = 200;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SlowAboveMs < FastBelowMs)
        {
            yield return new ValidationResult(
                "SlowAboveMs must be greater than or equal to FastBelowMs.",
                [nameof(SlowAboveMs)]);
        }
    }
}
