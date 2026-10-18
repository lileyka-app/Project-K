namespace ProjectK.Common.Models.Settings;

/// <summary>
/// Where «Повідомити про проблему» delivers. Bound from the <c>Feedback</c> section.
/// </summary>
public sealed class FeedbackOptions
{
    public GitHubFeedbackOptions GitHub { get; set; } = new();
}

public sealed class GitHubFeedbackOptions
{
    /// <summary>A fine-grained token with «Issues: write» on the repository. Empty means reports go to the log.</summary>
    public string? Token { get; set; }

    /// <summary><c>owner/name</c>.</summary>
    public string Repository { get; set; } = "lileyka-app/Project-K";

    /// <summary>Labels put on every issue the app opens, comma-separated.</summary>
    public string Labels { get; set; } = "bug,from client";

    public string BaseUrl { get; set; } = "https://api.github.com";
}
