using DataFusionAPI.Core.Models;

namespace DataFusionAPI.Core.Interfaces;

public interface IStatsRecorder
{
    void Record(string source, long elapsedMs);

    IReadOnlyDictionary<string, SourceStats> GetStats();
}
