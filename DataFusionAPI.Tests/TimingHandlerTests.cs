using System.Net;
using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Infrastructure.Http;
using Moq;

namespace DataFusionAPI.Tests;

public class TimingHandlerTests
{
    [Fact]
    public async Task SendAsync_RecordsElapsedTimeForSource()
    {
        var recorder = new Mock<IStatsRecorder>();
        var handler = new TimingHandler(recorder.Object, "GitHub")
        {
            InnerHandler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))
        };
        using var invoker = new HttpMessageInvoker(handler);

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.com"), CancellationToken.None);

        recorder.Verify(r => r.Record("GitHub", It.Is<long>(ms => ms >= 0)), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenInnerThrows_StillRecordsAndPropagates()
    {
        var recorder = new Mock<IStatsRecorder>();
        var handler = new TimingHandler(recorder.Object, "TMDB")
        {
            InnerHandler = new StubHttpMessageHandler(_ => throw new HttpRequestException("fail"))
        };
        using var invoker = new HttpMessageInvoker(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.com"), CancellationToken.None));

        recorder.Verify(r => r.Record("TMDB", It.IsAny<long>()), Times.Once);
    }
}
