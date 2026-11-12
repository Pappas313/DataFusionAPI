# DataFusionAPI

ASP.NET Core (.NET 10) service that aggregates data from multiple external APIs behind a single endpoint.
Requests are sent to all sources in parallel, results are merged, filtered and sorted, and failures of individual
sources are handled gracefully.

| Source    | API                              | Mapped as                                         |
|-----------|----------------------------------|---------------------------------------------------|
| GitHub    | Repository search                | category = language, score = GitHub search score  |
| CoinGecko | Coin search                      | category = `Crypto`, score = 1 / market-cap rank  |
| TMDB      | Multi search (movie, tv, person) | category = media type, score = popularity         |

## Architecture

```
DataFusionAPI                  ASP.NET Core host, controllers, Swagger
DataFusionAPI.Core             Models and interfaces (AggregatedItem, IExternalApiClient, IStatsRecorder, ...)
DataFusionAPI.Infrastructure   API clients, caching decorator, Polly resilience, timing handler, stats recorder, aggregation service
DataFusionAPI.Tests            xUnit + Moq tests
```

**Adding a new API:** implement `IExternalApiClient` (a `SourceName` and `FetchAsync`) and register it in `Program.cs`:

```csharp
builder.Services.AddExternalApiClient<MyApiClient>(MyApiClient.Name, client =>
	client.BaseAddress = new Uri("https://example.com/"));
```

The registration adds timing, retry, circuit breaker, timeout and caching automatically.

## Setup

Requires the .NET 10 SDK.

TMDB needs an API key (free from https://www.themoviedb.org/settings/api, use the v3 "API Key").
Put it in `DataFusionAPI/appsettings.json` under `Tmdb:ApiKey`, or better, keep it out of source control:

```powershell
dotnet user-secrets set "Tmdb:ApiKey" "<your key>" --project DataFusionAPI
```

The environment variable `Tmdb__ApiKey` also works. Without a key, TMDB is reported in `failedSources` and the other sources still work.

```powershell
dotnet run --project DataFusionAPI
dotnet test
```

In Development, Swagger UI is available at `/swagger`. `sortBy` and `sortOrder` are dropdowns there.

## Endpoints

### GET /api/aggregate

Fetches all sources in parallel, then filters and sorts the combined result. A failing source does not fail the
request; its name is returned in `failedSources`.

| Parameter   | Required | Values                                                      |
|-------------|----------|-------------------------------------------------------------|
| `query`     | yes      | Search term sent to every source                            |
| `category`  | no       | Case-insensitive, e.g. `Crypto`, `movie`, `tv`, `C#`        |
| `sortBy`    | no       | `Relevance` (default) or `Date`                             |
| `sortOrder` | no       | `Desc` (default) or `Asc`                                   |

Request:

```
GET /api/aggregate?query=bitcoin&category=Crypto&sortBy=Relevance&sortOrder=Desc
```

Response `200 OK`:

```json
{
  "items": [
	{
	  "id": "coingecko-bitcoin",
	  "source": "CoinGecko",
	  "title": "Bitcoin",
	  "description": "Symbol: BTC",
	  "url": "https://www.coingecko.com/en/coins/bitcoin",
	  "publishedOrRelevantDate": "2025-01-15T10:30:00+00:00",
	  "category": "Crypto",
	  "rawScore": 1.0
	}
  ],
  "failedSources": ["TMDB"]
}
```

Response `400 Bad Request` when `query` is missing or empty:

```json
{ "title": "Invalid request", "status": 400, "detail": "The 'query' parameter is required." }
```

### GET /api/stats

Per-source request statistics, held in memory (reset on restart). Buckets: fast < 100 ms, average 100-200 ms, slow > 200 ms.

```json
{
  "GitHub": {
	"totalRequests": 12,
	"averageResponseMs": 143.5,
	"fastCount": 3,
	"averageCount": 7,
	"slowCount": 2
  }
}
```

- Each outgoing HTTP attempt is counted, so retries count separately. Cache hits make no HTTP call and are not counted.
- Counters use lock-free `Interlocked` operations in constant memory per source.

## Resilience and caching

- Per HTTP attempt: 3 s timeout. Up to 2 retries with exponential backoff (200 ms, 400 ms) on 5xx, 408, network errors and timeouts.
- Circuit breaker opens after 3 consecutive failures for 30 s.
- Results are cached for 60 s per `{source}:{query}`. Concurrent requests for the same key share one upstream call.
- If a source fails, the last successful result (kept up to 24 h) is served instead; with no previous result the source is listed in `failedSources`.
  A stale result is served silently, so it is not listed in `failedSources`.

## Known limitations

- TMDB results without a date use `DateTimeOffset.MinValue`; CoinGecko items use the time of the request, since search results have no date.
- The TMDB key is sent as a query parameter (`api_key`), so it can appear in HTTP client logs at verbose levels.
- Optional features (JWT authentication, anomaly-logging background service) are not implemented.
