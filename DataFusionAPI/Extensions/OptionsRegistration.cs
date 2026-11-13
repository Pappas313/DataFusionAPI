using DataFusionAPI.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DataFusionAPI.Extensions;

public static class OptionsRegistration
{
    /// <summary>
    /// Binds and validates all settings. Invalid values stop the app at startup
    /// (ValidateOnStart) instead of failing on the first request.
    /// </summary>
    public static IServiceCollection AddAggregationOptions(this IServiceCollection services)
    {
        AddValidated<CacheOptions>(services, CacheOptions.Section);
        AddValidated<ResilienceOptions>(services, ResilienceOptions.Section);
        AddValidated<StatsOptions>(services, StatsOptions.Section);
        AddValidated<GitHubOptions>(services, GitHubOptions.Section);
        AddValidated<CoinGeckoOptions>(services, CoinGeckoOptions.Section);
        AddValidated<TmdbOptions>(services, TmdbOptions.Section);

        services.AddOptions<MemoryCacheOptions>()
            .Configure<IOptions<CacheOptions>>((memory, cache) => memory.SizeLimit = cache.Value.MaxEntries);

        return services;
    }

    private static void AddValidated<T>(IServiceCollection services, string section) where T : class
    {
        services.AddOptions<T>()
            .BindConfiguration(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
