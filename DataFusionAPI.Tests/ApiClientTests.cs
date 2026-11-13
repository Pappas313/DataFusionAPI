using System.Net;
using DataFusionAPI.Infrastructure.Clients;
using Microsoft.Extensions.Configuration;
using Moq;

namespace DataFusionAPI.Tests;

public class ApiClientTests
{
    private static HttpClient Http(StubHttpMessageHandler handler, string baseAddress) =>
        new(handler) { BaseAddress = new Uri(baseAddress) };

    [Fact]
    public async Task GitHub_MapsRepositories()
    {
        var handler = StubHttpMessageHandler.Json("""
            { "items": [ { "id": 7, "full_name": "dotnet/runtime", "description": "desc",
              "html_url": "https://github.com/dotnet/runtime", "language": "C#",
              "updated_at": "2025-01-15T10:30:00Z", "score": 1.5 } ] }
            """);
        var client = new GitHubApiClient(Http(handler, "https://api.github.com/"));

        var items = await client.FetchAsync("dot net", CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal("github-7", item.Id);
        Assert.Equal("GitHub", item.Source);
        Assert.Equal("dotnet/runtime", item.Title);
        Assert.Equal("C#", item.Category);
        Assert.Equal(1.5, item.RawScore);
        Assert.Contains("q=dot%20net", handler.LastRequest!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task GitHub_NonSuccessStatus_Throws()
    {
        var handler = StubHttpMessageHandler.Json("{}", HttpStatusCode.InternalServerError);
        var client = new GitHubApiClient(Http(handler, "https://api.github.com/"));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchAsync("q", CancellationToken.None));
    }

    [Fact]
    public async Task CoinGecko_MapsCoins()
    {
        var handler = StubHttpMessageHandler.Json("""
            { "coins": [ { "id": "bitcoin", "name": "Bitcoin", "symbol": "BTC", "market_cap_rank": 1 },
                         { "id": "obscure", "name": "Obscure", "symbol": "OBS", "market_cap_rank": null } ] }
            """);
        var client = new CoinGeckoApiClient(Http(handler, "https://api.coingecko.com/api/v3/"));

        var items = await client.FetchAsync("bit", CancellationToken.None);

        Assert.Equal(2, items.Count);
        Assert.Equal("coingecko-bitcoin", items[0].Id);
        Assert.Equal("Crypto", items[0].Category);
        Assert.Equal(1.0, items[0].RawScore);
        Assert.Equal(0, items[1].RawScore);
    }

    [Fact]
    public async Task CoinGecko_NonSuccessStatus_Throws()
    {
        var handler = StubHttpMessageHandler.Json("{}", HttpStatusCode.TooManyRequests);
        var client = new CoinGeckoApiClient(Http(handler, "https://api.coingecko.com/api/v3/"));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchAsync("q", CancellationToken.None));
    }

    private static Microsoft.Extensions.Options.IOptions<DataFusionAPI.Infrastructure.Options.TmdbOptions> Config(string? apiKey) =>
        Microsoft.Extensions.Options.Options.Create(
            new DataFusionAPI.Infrastructure.Options.TmdbOptions { ApiKey = apiKey ?? string.Empty });

    [Fact]
    public async Task Tmdb_MapsMovieAndTvResults_AndSendsApiKey()
    {
        var handler = StubHttpMessageHandler.Json("""
            { "results": [
              { "id": 1, "media_type": "movie", "title": "Matrix", "overview": "o1", "release_date": "1999-03-31", "popularity": 50.5 },
              { "id": 2, "media_type": "tv", "name": "Show", "overview": "o2", "first_air_date": "2010-01-01", "popularity": 5 },
              { "id": 3, "media_type": "person", "name": "Someone", "popularity": 1 } ] }
            """);
        var client = new TmdbApiClient(Http(handler, "https://api.themoviedb.org/3/"), Config("secret"));

        var items = await client.FetchAsync("matrix", CancellationToken.None);

        Assert.Equal(3, items.Count);
        Assert.Equal("Matrix", items[0].Title);
        Assert.Equal(new DateTimeOffset(1999, 3, 31, 0, 0, 0, TimeSpan.Zero), items[0].PublishedOrRelevantDate);
        Assert.Equal("Show", items[1].Title);
        Assert.Equal("tv", items[1].Category);
        Assert.Equal(DateTimeOffset.MinValue, items[2].PublishedOrRelevantDate);
        Assert.Contains("api_key=secret", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Tmdb_MissingApiKey_Throws()
    {
        var handler = StubHttpMessageHandler.Json("{}");
        var client = new TmdbApiClient(Http(handler, "https://api.themoviedb.org/3/"), Config(null));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.FetchAsync("q", CancellationToken.None));
    }

    [Fact]
    public async Task Tmdb_NonSuccessStatus_Throws()
    {
        var handler = StubHttpMessageHandler.Json("{}", HttpStatusCode.Unauthorized);
        var client = new TmdbApiClient(Http(handler, "https://api.themoviedb.org/3/"), Config("k"));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchAsync("q", CancellationToken.None));
    }
}
