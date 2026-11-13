using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace DataFusionAPI.Controllers;

/// <summary>
/// Aggregates data from all registered external APIs.
/// </summary>
[ApiController]
[Route("api/aggregate")]
public class AggregationController : ControllerBase
{
    private readonly IAggregationService _aggregationService;

    public AggregationController(IAggregationService aggregationService)
    {
        _aggregationService = aggregationService;
    }

    /// <summary>
    /// Fetches data from all external sources in parallel, then filters and sorts it.
    /// </summary>
    /// <param name="query">Search term sent to every source. Required.</param>
    /// <param name="category">Optional case-insensitive category filter (e.g. Crypto, movie, tv, or a GitHub language).</param>
    /// <param name="sortBy">Field to sort by. Defaults to Relevance.</param>
    /// <param name="sortOrder">Sort direction. Defaults to Desc.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The aggregated items and the names of sources that failed.</returns>
    /// <response code="200">Aggregated result. Failed sources are listed in <c>failedSources</c>.</response>
    /// <response code="400">The <c>query</c> parameter is missing or empty.</response>
    [HttpGet]
    [ProducesResponseType<AggregationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] string? query,
        [FromQuery] string? category,
        [FromQuery] SortField? sortBy,
        [FromQuery] SortDirection? sortOrder,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "The 'query' parameter is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _aggregationService.AggregateAsync(
            query,
            sortBy ?? SortField.Relevance,
            sortOrder ?? SortDirection.Desc,
            category,
            ct);

        return Ok(result);
    }
}
