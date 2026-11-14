namespace ProjectK.Common.Records.InfrastructureModule;

/// <summary>
/// What a person reports from inside the app, plus what the server knows about where they stood
/// when they did. Text fields are Markdown the client already composed; the reporter only frames
/// them. No name, no e-mail: the issue lands in a public tracker.
/// </summary>
public sealed record ProblemReport(
    string Title,
    string Description,
    string? Steps,
    string? Expected,
    string? Route,
    string? AppVersion,
    string? UserAgent,
    Guid ReporterUserKey,
    Guid? KurinKey,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> ScreenshotUrls);
