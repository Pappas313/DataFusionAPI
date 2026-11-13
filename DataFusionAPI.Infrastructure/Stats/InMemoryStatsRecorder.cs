using System.Collections.Concurrent;
using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Core.Models;
using DataFusionAPI.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace DataFusionAPI.Infrastructure.Stats;

/// <summary>
/// Thread-safe, lock-free in-memory statistics. Uses constant memory per source
/// (counters only) instead of storing every sample.
/// </summary>
public class InMemoryStatsRecorder : IStatsRecorder
{
    private readonly long _fastBelowMs;
    private readonly long _slowAboveMs;

    private readonly ConcurrentDictionary<string, Counters> _counters = new();

    public InMemoryStatsRecorder(IOptions<StatsOptions> options)
    {
        _fastBelowMs = options.Value.FastBelowMs;
        _slowAboveMs = options.Value.SlowAboveMs;
    }

    public void Record(string source, long elapsedMs)
    {
        var counters = _counters.GetOrAdd(source, _ => new Counters());

        Interlocked.Add(ref counters.TotalMs, elapsedMs);

        if (elapsedMs < _fastBelowMs)
        {
            Interlocked.Increment(ref counters.Fast);
        }
        else if (elapsedMs <= _slowAboveMs)
        {
            Interlocked.Increment(ref counters.Average);
        }
        else
        {
            Interlocked.Increment(ref counters.Slow);
        }
    }

    public IReadOnlyDictionary<string, SourceStats> GetStats()
    {
        var result = new Dictionary<string, SourceStats>();

        foreach (var (source, c) in _counters)
        {
            var fast = Interlocked.Read(ref c.Fast);
            var average = Interlocked.Read(ref c.Average);
            var slow = Interlocked.Read(ref c.Slow);
            var total = fast + average + slow;

            if (total == 0)
            {
                continue;
            }

            var totalMs = Interlocked.Read(ref c.TotalMs);
            result[source] = new SourceStats((int)total, (double)totalMs / total, (int)fast, (int)average, (int)slow);
        }

        return result;
    }

    private sealed class Counters
    {
        public long TotalMs;
        public long Fast;
        public long Average;
        public long Slow;
    }
}
