using System.Net;
using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Infrastructure.Caching;
using DataFusionAPI.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace DataFusionAPI.Infrastructure.Http;

public static class ExternalApiClientRegistration
{
    /// <summary>
    /// Registers a typed HttpClient for an external API with timing and resilience
    /// (total timeout -> retry with exponential backoff -> circuit breaker -> per-attempt timeout)
    /// and exposes it, wrapped in a cache, as <see cref="IAggregationSource"/>.
    /// All numbers come from <see cref="ResilienceOptions"/> and <see cref="CacheOptions"/>.
    /// </summary>
    public static IHttpClientBuilder AddExternalApiClient<TClient>(
        this IServiceCollection services,
        string sourceName,
        Action<IServiceProvider, HttpClient> configureClient)
        where TClient : class, IExternalApiClient
    {
        // One breaker instance per registered client so its state survives handler rotation.
        IAsyncPolicy<HttpResponseMessage>? circuitBreaker = null;

        var builder = services
            .AddHttpClient<TClient>(configureClient)
            // Order matters: the first handler is the outermost.
            .AddPolicyHandler((sp, _) => Policy.TimeoutAsync<HttpResponseMessage>(Resilience(sp).TotalTimeoutSeconds))
            .AddPolicyHandler((sp, _) => GetRetryPolicy(Resilience(sp)))
            .AddPolicyHandler((sp, _) => LazyInitializer.EnsureInitialized(
                ref circuitBreaker, () => GetCircuitBreakerPolicy(Resilience(sp))))
            .AddPolicyHandler((sp, _) => Policy.TimeoutAsync<HttpResponseMessage>(Resilience(sp).AttemptTimeoutSeconds))
            // Innermost: measures every actual HTTP attempt.
            .AddHttpMessageHandler(sp => new TimingHandler(sp.GetRequiredService<IStatsRecorder>(), sourceName));

        services.AddMemoryCache();
        services.AddTransient<IAggregationSource>(sp => new CachedExternalApiClient(
            sp.GetRequiredService<TClient>(),
            sp.GetRequiredService<IMemoryCache>(),
            sp.GetRequiredService<IOptions<CacheOptions>>()));

        return builder;
    }

    private static ResilienceOptions Resilience(IServiceProvider sp) =>
        sp.GetRequiredService<IOptions<ResilienceOptions>>().Value;

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ResilienceOptions o) =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                o.RetryCount,
                (attempt, outcome, _) =>
                {
                    var backoff = TimeSpan.FromMilliseconds(o.RetryBaseDelayMs * Math.Pow(2, attempt - 1));
                    var header = outcome.Result?.Headers.RetryAfter;
                    var retryAfter = header?.Delta ?? (header?.Date - DateTimeOffset.UtcNow);

                    // Long Retry-After values (GitHub can ask for 60 s) aren't worth holding a
                    // user request for; the stale cache fallback covers them.
                    return retryAfter is { } wait && wait >= TimeSpan.Zero
                        && wait <= TimeSpan.FromSeconds(o.MaxRetryAfterSeconds)
                        ? wait
                        : backoff;
                },
                (_, _, _, _) => Task.CompletedTask);

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ResilienceOptions o) =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: o.CircuitBreakerFailureRatio,
                samplingDuration: TimeSpan.FromSeconds(o.CircuitBreakerSamplingSeconds),
                minimumThroughput: o.CircuitBreakerMinimumThroughput,
                durationOfBreak: TimeSpan.FromSeconds(o.CircuitBreakDurationSeconds));
}
