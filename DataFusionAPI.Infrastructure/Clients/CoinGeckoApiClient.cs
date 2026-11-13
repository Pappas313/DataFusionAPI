using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Core.Models;

namespace DataFusionAPI.Infrastructure.Clients;

/// <summary>
/// Fetches coins from the CoinGecko search API.
/// The HttpClient must be configured with BaseAddress https://api.coingecko.com/api/v3/.
/// </summary>
public class CoinGeckoApiClient : IExternalApiClient
{
    public const string Name = "CoinGecko";

    private readonly HttpClient _httpClient;

    public CoinGeckoApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string SourceName => Name;

    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(string query, CancellationToken cancellationToken)
    {
        var url = $"search?query={Uri.EscapeDataString(query)}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken);
        return payload?.Coins?.Select(Map).ToList() ?? [];
    }

    private static AggregatedItem Map(Coin coin) => new(
        Id: $"coingecko-{coin.Id}",
        Source: Name,
        Title: coin.Name ?? string.Empty,
        Description: $"Symbol: {coin.Symbol}",
        Url: $"https://www.coingecko.com/en/coins/{coin.Id}",
        PublishedOrRelevantDate: null,
        Category: "Crypto",
        RawScore: coin.MarketCapRank is > 0 ? 1.0 / coin.MarketCapRank.Value : 0);

    private sealed class SearchResponse
    {
        [JsonPropertyName("coins")]
        public List<Coin>? Coins { get; set; }
    }

    private sealed class Coin
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("market_cap_rank")]
        public int? MarketCapRank { get; set; }
    }
}
