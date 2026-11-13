using DataFusionAPI.Infrastructure.Stats;

namespace DataFusionAPI.Tests;

public class InMemoryStatsRecorderTests
{
    [Fact]
    public void GetStats_NoRecords_ReturnsEmpty()
    {
        var recorder = new InMemoryStatsRecorder(Microsoft.Extensions.Options.Options.Create(new DataFusionAPI.Infrastructure.Options.StatsOptions()));

        Assert.Empty(recorder.GetStats());
    }

    [Fact]
    public void GetStats_ComputesTotalAndAverage()
    {
        var recorder = new InMemoryStatsRecorder(Microsoft.Extensions.Options.Options.Create(new DataFusionAPI.Infrastructure.Options.StatsOptions()));
        recorder.Record("GitHub", 50);
        recorder.Record("GitHub", 150);
        recorder.Record("GitHub", 400);

        var stats = recorder.GetStats()["GitHub"];

        Assert.Equal(3, stats.TotalRequests);
        Assert.Equal(200, stats.AverageResponseMs, precision: 5);
    }

    [Fact]
    public void GetStats_AssignsBucketsWithCorrectBoundaries()
    {
        var recorder = new InMemoryStatsRecorder(Microsoft.Extensions.Options.Options.Create(new DataFusionAPI.Infrastructure.Options.StatsOptions()));
        foreach (var ms in new long[] { 0, 99, 100, 150, 200, 201, 1000 })
        {
            recorder.Record("S", ms);
        }

        var stats = recorder.GetStats()["S"];

        Assert.Equal(2, stats.FastCount);
        Assert.Equal(3, stats.AverageCount);
        Assert.Equal(2, stats.SlowCount);
        Assert.Equal(stats.TotalRequests, stats.FastCount + stats.AverageCount + stats.SlowCount);
    }

    [Fact]
    public void GetStats_TracksSourcesIndependently()
    {
        var recorder = new InMemoryStatsRecorder(Microsoft.Extensions.Options.Options.Create(new DataFusionAPI.Infrastructure.Options.StatsOptions()));
        recorder.Record("A", 10);
        recorder.Record("B", 300);

        var stats = recorder.GetStats();

        Assert.Equal(1, stats["A"].FastCount);
        Assert.Equal(1, stats["B"].SlowCount);
    }

    [Fact]
    public void Record_ConcurrentCalls_DoesNotLoseRequests()
    {
        var recorder = new InMemoryStatsRecorder(Microsoft.Extensions.Options.Options.Create(new DataFusionAPI.Infrastructure.Options.StatsOptions()));
        const int total = 10_000;

        Parallel.For(0, total, i => recorder.Record("Concurrent", i % 300));

        var stats = recorder.GetStats()["Concurrent"];

        Assert.Equal(total, stats.TotalRequests);
        Assert.Equal(total, stats.FastCount + stats.AverageCount + stats.SlowCount);
    }
}

