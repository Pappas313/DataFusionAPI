using System.ComponentModel.DataAnnotations;

namespace DataFusionAPI.Infrastructure.Options;

public sealed class TmdbOptions
{
    public const string Section = "Tmdb";

    /// <summary>Optional at startup: when empty, TMDB is reported in failedSources and a warning is logged.</summary>
    public string ApiKey { get; init; } = string.Empty;

    [Required]
    public Uri BaseAddress { get; init; } = new("https://api.themoviedb.org/3/");
}
