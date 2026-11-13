using System.ComponentModel.DataAnnotations;

namespace DataFusionAPI.Infrastructure.Options;

public sealed class GitHubOptions
{
    public const string Section = "GitHub";

    /// <summary>Optional personal access token; raises the GitHub rate limit when set.</summary>
    public string Token { get; init; } = string.Empty;

    [Required]
    public Uri BaseAddress { get; init; } = new("https://api.github.com/");
}
