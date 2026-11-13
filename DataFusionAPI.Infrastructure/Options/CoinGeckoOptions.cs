using System.ComponentModel.DataAnnotations;

namespace DataFusionAPI.Infrastructure.Options;

public sealed class CoinGeckoOptions
{
    public const string Section = "CoinGecko";

    /// <summary>Optional CoinGecko demo API key, sent as the x-cg-demo-api-key header.</summary>
    public string DemoApiKey { get; init; } = string.Empty;

    [Required]
    public Uri BaseAddress { get; init; } = new("https://api.coingecko.com/api/v3/");
}
