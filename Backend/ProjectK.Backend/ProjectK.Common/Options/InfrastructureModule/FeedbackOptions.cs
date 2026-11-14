namespace ProjectK.Common.Options.InfrastructureModule;

/// <summary>
/// Where «Повідомити про проблему» delivers. Bound from the <c>Feedback</c> section.
/// </summary>
public sealed class FeedbackOptions
{
    public GitHubFeedbackOptions GitHub { get; set; } = new();
}
