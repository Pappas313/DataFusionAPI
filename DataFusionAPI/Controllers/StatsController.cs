using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace DataFusionAPI.Controllers;

/// <summary>
/// Request statistics for external APIs.
/// </summary>
[ApiController]
[Route("api/stats")]
public class StatsController : ControllerBase
{
    private readonly IStatsRecorder _statsRecorder;

    public StatsController(IStatsRecorder statsRecorder)
    {
        _statsRecorder = statsRecorder;
    }

    /// <summary>
    /// Returns, per external API, the total requests, average response time and performance buckets
    /// (fast &lt; 100ms, average 100-200ms, slow &gt; 200ms).
    /// </summary>
    /// <response code="200">Statistics keyed by source name.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyDictionary<string, SourceStats>>(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(_statsRecorder.GetStats());
}
