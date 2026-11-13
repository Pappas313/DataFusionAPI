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

    [Range(0, 60)]
    public int MaxRetryAfterSeconds { get; init; } = 2;

    [Range(0.01, 1.0)]
    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    [Range(1, 3_600)]
    public int CircuitBreakerSamplingSeconds { get; init; } = 30;

    [Range(2, 1_000)]
    public int CircuitBreakerMinimumThroughput { get; init; } = 6;

    [Range(1, 3_600)]
    public int CircuitBreakDurationSeconds { get; init; } = 15;

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
