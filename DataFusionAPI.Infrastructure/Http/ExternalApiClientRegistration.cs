using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace DataFusionAPI.Infrastructure.Http;

public static class ExternalApiClientRegistration
{
    /// <summary>
    /// Registers a typed HttpClient for an external API with timing and resilience
    /// (retry with exponential backoff -> circuit breaker -> 3s timeout per attempt)
    /// and exposes it as <see cref="IExternalApiClient"/>.
    /// </summary>
    public static IHttpClientBuilder AddExternalApiClient<TClient>(
        this IServiceCollection services,
        string sourceName,
        Action<HttpClient> configureClient)
        where TClient : class, IExternalApiClient
    {
        var builder = services
            .AddHttpClient<TClient>(configureClient)
            // Order matters: the first handler is the outermost.
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy())
            .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(3)))
            // Innermost: measures every actual HTTP attempt.
            .AddHttpMessageHandler(sp => new TimingHandler(sp.GetRequiredService<IStatsRecorder>(), sourceName));

        services.AddMemoryCache();
        services.AddTransient<IExternalApiClient>(sp => new CachedExternalApiClient(sp.GetRequiredService<TClient>(), sp.GetRequiredService<IMemoryCache>()));

        return builder;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(2, attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)));

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .CircuitBreakerAsync(3, TimeSpan.FromSeconds(30));
}
