using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Core.Models;

namespace DataFusionAPI.Infrastructure.Clients;

/// <summary>
/// Fetches repositories from the GitHub search API.
/// The HttpClient must be configured with BaseAddress https://api.github.com/ and a User-Agent header.
/// </summary>
public class GitHubApiClient : IExternalApiClient
{
    public const string Name = "GitHub";

    private readonly HttpClient _httpClient;

    public GitHubApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string SourceName => Name;

    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(string query, CancellationToken cancellationToken)
    {
        var url = $"search/repositories?q={Uri.EscapeDataString(query)}&per_page=10";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken);

        return payload?.Items?.Select(Map).ToList() ?? [];
    }

    private static AggregatedItem Map(Repository repo) => new(
        Id: $"github-{repo.Id}",
        Source: Name,
        Title: repo.FullName ?? string.Empty,
        Description: repo.Description ?? string.Empty,
        Url: repo.HtmlUrl ?? string.Empty,
        PublishedOrRelevantDate: repo.UpdatedAt,
        Category: repo.Language ?? "Repository",
        RawScore: repo.Score);

    private sealed class SearchResponse
    {
        [JsonPropertyName("items")]
        public List<Repository>? Items { get; set; }
    }

    private sealed class Repository
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("full_name")]
        public string? FullName { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }
}
