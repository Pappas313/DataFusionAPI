using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Core.Models;
using Microsoft.Extensions.Configuration;

namespace DataFusionAPI.Infrastructure.Clients;

/// <summary>
/// Fetches movies, TV shows and people from TMDB's multi-search endpoint.
/// The HttpClient must be configured with BaseAddress https://api.themoviedb.org/3/.
/// The API key is read from configuration key "Tmdb:ApiKey".
/// </summary>
public class TmdbApiClient : IExternalApiClient
{
    public const string Name = "TMDB";

    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public TmdbApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Tmdb:ApiKey"];
    }

    public string SourceName => Name;

    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("TMDB API key is not configured (Tmdb:ApiKey).");
        }

        var url = $"search/multi?query={Uri.EscapeDataString(query)}&api_key={Uri.EscapeDataString(_apiKey)}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken);

        return payload?.Results?.Select(Map).ToList() ?? [];
    }

    private static AggregatedItem Map(Result r)
    {
        var mediaType = r.MediaType ?? "unknown";
        var dateText = !string.IsNullOrEmpty(r.ReleaseDate) ? r.ReleaseDate : r.FirstAirDate;

        var date = DateTimeOffset.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;

        return new AggregatedItem(
            Id: $"tmdb-{mediaType}-{r.Id}",
            Source: Name,
            Title: r.Title ?? r.Name ?? string.Empty,
            Description: r.Overview ?? string.Empty,
            Url: $"https://www.themoviedb.org/{mediaType}/{r.Id}",
            PublishedOrRelevantDate: date,
            Category: mediaType,
            RawScore: r.Popularity);
    }

    private sealed class SearchResponse
    {
        [JsonPropertyName("results")]
        public List<Result>? Results { get; set; }
    }

    private sealed class Result
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("media_type")]
        public string? MediaType { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("overview")]
        public string? Overview { get; set; }

        [JsonPropertyName("release_date")]
        public string? ReleaseDate { get; set; }

        [JsonPropertyName("first_air_date")]
        public string? FirstAirDate { get; set; }

        [JsonPropertyName("popularity")]
        public double Popularity { get; set; }
    }
}
