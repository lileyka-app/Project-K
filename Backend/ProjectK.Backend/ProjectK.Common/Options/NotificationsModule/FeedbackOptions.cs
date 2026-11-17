namespace ProjectK.Common.Options.NotificationsModule;

/// <summary>
/// Where «Повідомити про проблему» delivers. Bound from the <c>Feedback</c> section.
/// </summary>
public sealed class FeedbackOptions
{
    public const string SectionName = "Feedback";

    public GitHubFeedbackOptions GitHub { get; set; } = new();
}
