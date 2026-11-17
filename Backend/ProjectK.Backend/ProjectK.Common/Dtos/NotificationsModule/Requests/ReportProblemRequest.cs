namespace ProjectK.Common.Dtos.NotificationsModule.Requests;

/// <summary>Body of <c>POST api/feedback/problems</c>.</summary>
public sealed record ReportProblemRequest(
    string? Title,
    string Description,
    string? Steps,
    string? Expected,
    string? Route,
    string? AppVersion,
    IReadOnlyCollection<string>? ScreenshotUrls);
