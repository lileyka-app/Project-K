using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Features.Feedback;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.InfrastructureModule.Feedback;

/// <summary>
/// «Повідомити про проблему»: what the server adds, what it refuses, and what it never lets into a
/// public issue.
/// </summary>
public class ReportProblemCommandHandlerTests
{
    private readonly Mock<IProblemReporter> _reporter = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Mock<IActivityLogger> _activityLogger = new();
    private readonly Guid _userKey = Guid.NewGuid();
    private readonly Guid _kurinKey = Guid.NewGuid();

    public ReportProblemCommandHandlerTests()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(_userKey);
        _currentUser.SetupGet(c => c.KurinKey).Returns(_kurinKey);
        _currentUser.SetupGet(c => c.Roles).Returns(["Manager"]);
        _reporter
            .Setup(r => r.ReportAsync(It.IsAny<ProblemReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProblemReportReceipt("https://github.com/x/y/issues/7", 7));
    }

    private ReportProblemCommandHandler Handler() =>
        new(_reporter.Object, _currentUser.Object, _activityLogger.Object, NullLogger<ReportProblemCommandHandler>.Instance);

    private static ReportProblemCommand Command(
        string description = "Кнопка «Зберегти» нічого не робить.",
        string? title = null,
        IReadOnlyCollection<string>? screenshots = null) =>
        new(title, description, "1. Відкрити картку\n2. Натиснути", "Картка зберігається", "/member/abc", "v1.0.0", "Mozilla/5.0", screenshots);

    [Fact]
    public async Task Handle_ShouldFrameTheReportWithWhoAndWhere_AndAnswerWithTheIssue()
    {
        ProblemReport? sent = null;
        _reporter
            .Setup(r => r.ReportAsync(It.IsAny<ProblemReport>(), It.IsAny<CancellationToken>()))
            .Callback((ProblemReport p, CancellationToken _) => sent = p)
            .ReturnsAsync(new ProblemReportReceipt("https://github.com/x/y/issues/7", 7));

        var result = await Handler().Handle(Command(), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(7, result.Data!.IssueNumber);
        Assert.NotNull(sent);
        Assert.Equal(_userKey, sent!.ReporterUserKey);
        Assert.Equal(_kurinKey, sent.KurinKey);
        Assert.Contains("Manager", sent.Roles);
        Assert.Equal("Mozilla/5.0", sent.UserAgent);
        Assert.Equal("Кнопка «Зберегти» нічого не робить.", sent.Title);
        _activityLogger.Verify(a => a.LogAudit("Feedback.ProblemReported", _userKey, null, null, null, "https://github.com/x/y/issues/7"), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAnEmptyDescription()
    {
        var result = await Handler().Handle(Command(description: "   "), CancellationToken.None);

        Assert.Equal(ResultType.BadRequest, result.Type);
        Assert.Equal("DescriptionRequired", result.ErrorCode);
        _reporter.Verify(r => r.ReportAsync(It.IsAny<ProblemReport>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Only pictures the upload endpoint stored may be embedded; a report is not a way to put an
    // arbitrary image into a public tracker.
    [Fact]
    public async Task Handle_ShouldKeepOnlyOurScreenshots()
    {
        ProblemReport? sent = null;
        _reporter
            .Setup(r => r.ReportAsync(It.IsAny<ProblemReport>(), It.IsAny<CancellationToken>()))
            .Callback((ProblemReport p, CancellationToken _) => sent = p)
            .ReturnsAsync(new ProblemReportReceipt(null, null));

        await Handler().Handle(Command(screenshots:
        [
            "https://blob.example/photos/feedback-screenshots/a.png",
            "https://blob.example/photos/feedback-screenshots/a.png",
            "https://blob.example/photos/member-photos/b.jpg",
            "https://evil.example/tracker.gif",
            "javascript:alert(1)"
        ]), CancellationToken.None);

        Assert.Equal(["https://blob.example/photos/feedback-screenshots/a.png"], sent!.ScreenshotUrls);
    }

    [Fact]
    public async Task Handle_ShouldTellThePersonToRetry_WhenTheTrackerIsDown()
    {
        _reporter
            .Setup(r => r.ReportAsync(It.IsAny<ProblemReport>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("GitHub answered 503."));

        var result = await Handler().Handle(Command(), CancellationToken.None);

        Assert.Equal(ResultType.InternalServerError, result.Type);
        Assert.Equal("ReportNotDelivered", result.ErrorCode);
    }

    [Fact]
    public void TitleFor_ShouldTakeTheFirstLine_AndCutToFit()
    {
        Assert.Equal("Перший рядок", ReportProblemCommandHandler.TitleFor(null, "# Перший рядок\nдругий"));
        Assert.Equal("Мій заголовок", ReportProblemCommandHandler.TitleFor("  Мій заголовок ", "щось"));
        var cut = ReportProblemCommandHandler.TitleFor(null, new string('а', 300));
        Assert.Equal(ReportProblemCommandHandler.MaxTitleLength, cut.Length);
        Assert.EndsWith("…", cut);
        Assert.Equal("Повідомлення з застосунку", ReportProblemCommandHandler.TitleFor(null, "---"));
    }
}
