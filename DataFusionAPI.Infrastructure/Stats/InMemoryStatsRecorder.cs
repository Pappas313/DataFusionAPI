using System.Collections.Concurrent;
using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Core.Models;

namespace DataFusionAPI.Infrastructure.Stats;

public class InMemoryStatsRecorder : IStatsRecorder
{
    private const long FastThresholdMs = 100;
    private const long SlowThresholdMs = 200;

    private readonly ConcurrentDictionary<string, ConcurrentQueue<long>> _timings = new();

    public void Record(string source, long elapsedMs)
    {
        _timings.GetOrAdd(source, _ => new ConcurrentQueue<long>()).Enqueue(elapsedMs);
    }

    public IReadOnlyDictionary<string, SourceStats> GetStats()
    {
        var result = new Dictionary<string, SourceStats>();

        foreach (var (source, queue) in _timings)
        {
            var samples = queue.ToArray();
            if (samples.Length == 0)
            {
                continue;
            }

            int fast = 0, average = 0, slow = 0;
            foreach (var ms in samples)
            {
                if (ms < FastThresholdMs) fast++;
                else if (ms <= SlowThresholdMs) average++;
                else slow++;
            }

            result[source] = new SourceStats(samples.Length, samples.Average(), fast, average, slow);
        }

        return result;
    }
}
