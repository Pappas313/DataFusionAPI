using System.Diagnostics;
using DataFusionAPI.Core.Interfaces;

namespace DataFusionAPI.Infrastructure.Http;

/// <summary>
/// Measures the duration of each outgoing HTTP call and reports it to <see cref="IStatsRecorder"/>.
/// Add it per named HttpClient, e.g.
/// <c>.AddHttpMessageHandler(sp =&gt; new TimingHandler(sp.GetRequiredService&lt;IStatsRecorder&gt;(), "GitHub"))</c>.
/// </summary>
public class TimingHandler : DelegatingHandler
{
    private readonly IStatsRecorder _statsRecorder;
    private readonly string _sourceName;

    public TimingHandler(IStatsRecorder statsRecorder, string sourceName)
    {
        _statsRecorder = statsRecorder;
        _sourceName = sourceName;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            stopwatch.Stop();
            _statsRecorder.Record(_sourceName, stopwatch.ElapsedMilliseconds);
        }
    }
}
