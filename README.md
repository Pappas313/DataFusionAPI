# DataFusionAPI

ASP.NET Core (.NET 10) service that aggregates data from multiple external APIs behind a single endpoint.
Currently integrated: **GitHub** repository search. New sources implement `IExternalApiClient` and are registered with
`AddExternalApiClient<T>(...)` in `Program.cs`.

## Setup

```powershell
dotnet run --project DataFusionAPI
```

In Development, Swagger UI is available at `/swagger`.

## Endpoints

### GET /api/aggregate

Fetches all sources in parallel, then filters and sorts the combined result. A failing source does not fail the
request; its name is returned in `failedSources`.

| Parameter   | Required | Description                                          |
|-------------|----------|------------------------------------------------------|
| `query`     | yes      | Search term sent to every source                     |
| `category`  | no       | Case-insensitive category filter (e.g. `C#`)         |
| `sortBy`    | no       | `date` or `relevance` (default)                      |
| `sortOrder` | no       | `asc` or `desc` (default)                            |

Request:

```
GET /api/aggregate?query=dotnet&category=C%23&sortBy=date&sortOrder=desc
```

Response `200 OK`:

```json
{
  "items": [
	{
	  "id": "github-1234",
	  "source": "GitHub",
	  "title": "dotnet/runtime",
	  "description": ".NET is a cross-platform runtime",
	  "url": "https://github.com/dotnet/runtime",
	  "publishedOrRelevantDate": "2025-01-15T10:30:00+00:00",
	  "category": "C#",
	  "rawScore": 1.0
	}
  ],
  "failedSources": []
}
```

Response `400 Bad Request` when `query` is missing or empty:

```json
{ "title": "Invalid request", "status": 400, "detail": "The 'query' parameter is required." }
```

### GET /api/stats

Per-source request statistics, held in memory. Buckets: fast < 100 ms, average 100-200 ms, slow > 200 ms.

Request:

```
GET /api/stats
```

Response `200 OK`:

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

## Resilience and caching

- Per request: 3 s timeout, 2 retries with exponential backoff, circuit breaker (opens after 3 consecutive failures for 30 s).
- Results are cached for 60 s per `{source}:{query}`. If a source fails, the last known result is served when available.
