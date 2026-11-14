using MediatR;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.InfrastructureModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.InfrastructureModule;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Features.Feedback;

/// <summary>
/// «Повідомити про проблему» from the sidebar. The text fields arrive as Markdown the client
/// composed from its editor; <paramref name="ScreenshotUrls"/> are what
/// <c>UploadFeedbackScreenshotCommand</c> handed back, nothing else is accepted.
/// </summary>
public sealed record ReportProblemCommand(
    string? Title,
    string Description,
    string? Steps,
    string? Expected,
    string? Route,
    string? AppVersion,
    string? UserAgent,
    IReadOnlyCollection<string>? ScreenshotUrls) : IRequest<ServiceResult<ProblemReportReceipt>>;

public sealed class ReportProblemCommandHandler
    : IRequestHandler<ReportProblemCommand, ServiceResult<ProblemReportReceipt>>
{
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionLength = 8000;
    public const int MaxSecondaryLength = 4000;
    public const int MaxScreenshots = 5;

    private readonly IProblemReporter _reporter;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActivityLogger _activityLogger;
    private readonly ILogger<ReportProblemCommandHandler> _logger;

    public ReportProblemCommandHandler(
        IProblemReporter reporter,
        ICurrentUserContext currentUser,
        IActivityLogger activityLogger,
        ILogger<ReportProblemCommandHandler> logger)
    {
        _reporter = reporter;
        _currentUser = currentUser;
        _activityLogger = activityLogger;
        _logger = logger;
    }

    public async Task<ServiceResult<ProblemReportReceipt>> Handle(ReportProblemCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } reporterKey)
        {
            return ServiceResult<ProblemReportReceipt>.Failure(ResultType.Unauthorized, InfrastructureErrors.SignInRequired, "Sign in to report a problem.");
        }

        var description = request.Description?.Trim() ?? string.Empty;
        if (description.Length == 0)
        {
            return ServiceResult<ProblemReportReceipt>.Failure(ResultType.BadRequest, InfrastructureErrors.DescriptionRequired, "Describe what happened.");
        }

        if (description.Length > MaxDescriptionLength)
        {
            return ServiceResult<ProblemReportReceipt>.Failure(ResultType.BadRequest, InfrastructureErrors.DescriptionTooLong, $"The description must be {MaxDescriptionLength} characters or fewer.");
        }

        var steps = Trimmed(request.Steps);
        var expected = Trimmed(request.Expected);
        if (steps?.Length > MaxSecondaryLength || expected?.Length > MaxSecondaryLength)
        {
            return ServiceResult<ProblemReportReceipt>.Failure(ResultType.BadRequest, InfrastructureErrors.FieldTooLong, $"Steps and expectation must be {MaxSecondaryLength} characters or fewer each.");
        }

        var screenshots = (request.ScreenshotUrls ?? [])
            .Where(IsOurScreenshot)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (screenshots.Length > MaxScreenshots)
        {
            return ServiceResult<ProblemReportReceipt>.Failure(ResultType.BadRequest, InfrastructureErrors.TooManyScreenshots, $"Attach at most {MaxScreenshots} screenshots.");
        }

        var report = new ProblemReport(
            Title: TitleFor(request.Title, description),
            Description: description,
            Steps: steps,
            Expected: expected,
            Route: Trimmed(request.Route),
            AppVersion: Trimmed(request.AppVersion),
            UserAgent: Trimmed(request.UserAgent),
            ReporterUserKey: reporterKey,
            KurinKey: _currentUser.KurinKey,
            Roles: _currentUser.Roles,
            ScreenshotUrls: screenshots);

        ProblemReportReceipt receipt;
        try
        {
            receipt = await _reporter.ReportAsync(report, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The text is not lost: it is in the log with the exception, and the person is told to
            // try again rather than shown a stack trace.
            _logger.LogError(ex, "Problem report from {UserKey} could not be delivered. Title: {Title}", reporterKey, report.Title);
            return ServiceResult<ProblemReportReceipt>.Failure(ResultType.InternalServerError, InfrastructureErrors.ReportNotDelivered, "The report could not be delivered. Try again in a minute.");
        }

        _activityLogger.LogAudit(
            action: "Feedback.ProblemReported",
            actorUserId: reporterKey,
            reason: receipt.IssueUrl ?? "logged");

        return new ServiceResult<ProblemReportReceipt>(ResultType.Success, receipt);
    }

    /// <summary>The first line of the description when no title was typed, cut to fit.</summary>
    public static string TitleFor(string? title, string description)
    {
        var chosen = Trimmed(title);
        if (chosen is null)
        {
            var firstLine = description.Split('\n', 2)[0].Trim().TrimStart('#', ' ', '>', '-', '*');
            chosen = firstLine.Length == 0 ? "Повідомлення з застосунку" : firstLine;
        }

        return chosen.Length <= MaxTitleLength ? chosen : chosen[..(MaxTitleLength - 1)] + "…";
    }

    /// <summary>
    /// Only what the upload endpoint produced may be embedded: the folder is ours and the scheme
    /// is web. Anything else would let a report put an arbitrary picture into a public issue.
    /// </summary>
    public static bool IsOurScreenshot(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && uri.AbsolutePath.Contains($"/{BlobUploadFolders.FeedbackScreenshots}/", StringComparison.Ordinal);

    private static string? Trimmed(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
