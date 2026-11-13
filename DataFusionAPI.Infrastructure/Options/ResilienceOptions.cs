using System.ComponentModel.DataAnnotations;

namespace DataFusionAPI.Infrastructure.Options;

public sealed class ResilienceOptions : IValidatableObject
{
    public const string Section = "Aggregation:Resilience";

    [Range(1, 60)]
    public int AttemptTimeoutSeconds { get; init; } = 3;

    [Range(1, 300)]
    public int TotalTimeoutSeconds { get; init; } = 5;

    [Range(0, 10)]
    public int RetryCount { get; init; } = 2;

    [Range(1, 10_000)]
    public int RetryBaseDelayMs { get; init; } = 200;

    [Range(1, 100)]
    public int CircuitBreakerFailures { get; init; } = 3;

    [Range(1, 3_600)]
    public int CircuitBreakDurationSeconds { get; init; } = 30;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TotalTimeoutSeconds < AttemptTimeoutSeconds)
        {
            yield return new ValidationResult(
                "TotalTimeoutSeconds must be greater than or equal to AttemptTimeoutSeconds.",
                [nameof(TotalTimeoutSeconds)]);
        }
    }
}
