using Moq;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge.Review;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.BusinessLogic.Services.Audit;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.ProbesAndBadgesModule.HandlerTests;

public class ReviewBadgeProgressHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMemberDirectory> _memberDirectoryMock;
    private readonly Mock<IBadgeProgressRepository> _badgeProgressRepositoryMock;
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IDomainEventPublisher> _eventsMock;
    private readonly ReviewBadgeProgressCommandHandler _handler;
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly JournalRecorder _journal;

    public ReviewBadgeProgressHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _memberDirectoryMock = new Mock<IMemberDirectory>();
        _badgeProgressRepositoryMock = new Mock<IBadgeProgressRepository>();
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _eventsMock = new Mock<IDomainEventPublisher>();
        _unitOfWorkMock.SetupGet(x => x.BadgeProgresses).Returns(_badgeProgressRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _currentUserContextMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _currentUserContextMock
            .Setup(x => x.IsInRole(It.IsAny<string>()))
            .Returns((string role) => string.Equals(role, "mentor", StringComparison.OrdinalIgnoreCase));
        _currentUserContextMock.SetupGet(x => x.Roles).Returns(new[] { "mentor" });
        _currentUserContextMock.SetupGet(x => x.KurinKey).Returns(_kurinKey);

        _journal = new JournalRecorder(
            _currentUserContextMock.Object,
            new AuditActorResolver(_currentUserContextMock.Object, _memberDirectoryMock.Object));

        _handler = new ReviewBadgeProgressCommandHandler(
            _unitOfWorkMock.Object,
            _memberDirectoryMock.Object,
            _currentUserContextMock.Object,
            _eventsMock.Object,
            _journal.Actors,
            _journal.Audit,
            _journal.Reader,
            Mock.Of<IBadgesCatalogService>());
    }

    [Theory]
    [InlineData(BadgeProgressStatus.Submitted, true, AuditActions.Badge.Confirmed, "Confirmed")]
    [InlineData(BadgeProgressStatus.Submitted, false, AuditActions.Badge.Rejected, "Rejected")]
    [InlineData(BadgeProgressStatus.Confirmed, false, AuditActions.Badge.ConfirmationRemoved, "Rejected")]
    public async Task Handle_ShouldRecordTheDecisionInTheJournal(BadgeProgressStatus from, bool approved, string action, string to)
    {
        var memberKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        _journal.Knows(memberKey, _kurinKey, groupKey, "Олесь", "Юнак");
        var progress = CreateProgress(memberKey, "badge-1", from);
        _badgeProgressRepositoryMock
            .Setup(x => x.GetByMemberAndBadgeIdAsync(memberKey, "badge-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);

        await _handler.Handle(new ReviewBadgeProgressCommand(memberKey, "badge-1", approved, "так"), CancellationToken.None);

        var entry = Assert.Single(_journal.Entries);
        Assert.Equal(action, entry.Action);
        Assert.Equal(progress.BadgeProgressKey, entry.SubjectKey);
        Assert.Equal("badge-1", entry.SubjectLabel);
        Assert.Equal(groupKey, entry.SubjectGroupKey);
        Assert.Equal("так", entry.Note);
        Assert.Equal(new AuditChange("status", from.ToString(), to), Assert.Single(entry.Changes));
    }

    /// <summary>
    /// The trail says who signed it off, by name. It used to say <c>UserId.ToString()</c> — the
    /// screens printed the guid, and nobody noticed because it never failed, it only read as
    /// gibberish. Found on the stabilisation pass, not by a test.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldRecordTheReviewersName_NotTheirKey()
    {
        var memberKey = Guid.NewGuid();
        var reviewerUserKey = Guid.NewGuid();
        const string badgeId = "badge-1";

        _currentUserContextMock.SetupGet(x => x.UserId).Returns(reviewerUserKey);
        _memberDirectoryMock
            .Setup(x => x.FindByAccountAsync(reviewerUserKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(
                Guid.NewGuid(), reviewerUserKey, Guid.NewGuid(), null,
                "Іван", "Петренко", "ivan@example.com", null));

        var progress = CreateProgress(memberKey, badgeId, BadgeProgressStatus.Submitted);
        _badgeProgressRepositoryMock
            .Setup(x => x.GetByMemberAndBadgeIdAsync(memberKey, badgeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);

        await _handler.Handle(new ReviewBadgeProgressCommand(memberKey, badgeId, true, null), CancellationToken.None);

        Assert.Equal("Іван Петренко", progress.ReviewedByName);
        Assert.DoesNotContain(reviewerUserKey.ToString(), progress.ReviewedByName);
        Assert.Equal("Іван Петренко", Assert.Single(_journal.Entries).ActorName);
    }

    /// <summary>
    /// A reviewer with no member record still has to be recorded as somebody. The marker says so in
    /// as many words, rather than leaving a bare guid that reads like a name gone wrong.
    /// </summary>
    [Fact]
    public async Task Handle_WhenTheReviewerHasNoMemberRecord_ShouldSaySoRatherThanPrintABareKey()
    {
        var memberKey = Guid.NewGuid();
        var reviewerUserKey = Guid.NewGuid();
        const string badgeId = "badge-1";

        _currentUserContextMock.SetupGet(x => x.UserId).Returns(reviewerUserKey);
        _memberDirectoryMock
            .Setup(x => x.FindByAccountAsync(reviewerUserKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemberSummary?)null);

        var progress = CreateProgress(memberKey, badgeId, BadgeProgressStatus.Submitted);
        _badgeProgressRepositoryMock
            .Setup(x => x.GetByMemberAndBadgeIdAsync(memberKey, badgeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);

        await _handler.Handle(new ReviewBadgeProgressCommand(memberKey, badgeId, true, null), CancellationToken.None);

        Assert.Equal($"user:{reviewerUserKey}", progress.ReviewedByName);
    }

    [Fact]
    public async Task Handle_ShouldApproveSubmittedProgress()
    {
        // Arrange
        var memberKey = Guid.NewGuid();
        var badgeId = "badge-1";
        var progress = CreateProgress(memberKey, badgeId, BadgeProgressStatus.Submitted);

        _badgeProgressRepositoryMock
            .Setup(x => x.GetByMemberAndBadgeIdAsync(memberKey, badgeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);

        var request = new ReviewBadgeProgressCommand(memberKey, badgeId, isApproved: true, note: null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(BadgeProgressStatus.Confirmed, progress.Status);
        Assert.NotNull(progress.ReviewedAtUtc);
        var trail = Assert.Single(result.Data!.AuditTrail);
        Assert.Equal(BadgeProgressStatus.Submitted, trail.FromStatus);
        Assert.Equal(BadgeProgressStatus.Confirmed, trail.ToStatus);
        Assert.Equal("Confirmed", trail.Action);
        Assert.Equal(Assert.Single(_journal.Entries).KurinAuditEventKey, trail.BadgeProgressAuditEventKey);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotifyMemberOwner_WhenSubmittedProgressIsApproved()
    {
        // Arrange
        var actorUserKey = Guid.NewGuid();
        var ownerUserKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();
        var badgeId = "badge-review-approved";
        var progress = CreateProgress(memberKey, badgeId, BadgeProgressStatus.Submitted);

        _currentUserContextMock.SetupGet(x => x.UserId).Returns(actorUserKey);
        _badgeProgressRepositoryMock
            .Setup(x => x.GetByMemberAndBadgeIdAsync(memberKey, badgeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);
        _memberDirectoryMock
            .Setup(x => x.FindAccountKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownerUserKey);

        var request = new ReviewBadgeProgressCommand(memberKey, badgeId, isApproved: true, note: null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        _eventsMock.Verify(x => x.PublishAsync(
            It.Is<BadgeProgressReviewed>(raised =>
                raised.BadgeProgressKey == progress.BadgeProgressKey
                && raised.BadgeId == badgeId
                && raised.MemberKey == memberKey
                && raised.MemberUserKey == ownerUserKey
                && raised.IsApproved
                && !raised.ConfirmationWithdrawn
                && raised.ActorUserKey == actorUserKey),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRemoveConfirmedProgress_WhenRequestedAsReject()
    {
        // Arrange
        var memberKey = Guid.NewGuid();
        var badgeId = "badge-2";
        var progress = CreateProgress(memberKey, badgeId, BadgeProgressStatus.Confirmed);

        _badgeProgressRepositoryMock
            .Setup(x => x.GetByMemberAndBadgeIdAsync(memberKey, badgeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);

        var request = new ReviewBadgeProgressCommand(memberKey, badgeId, isApproved: false, note: "remove");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(BadgeProgressStatus.Rejected, progress.Status);
        Assert.NotNull(progress.ReviewedAtUtc);
        var trail = Assert.Single(result.Data!.AuditTrail);
        Assert.Equal(BadgeProgressStatus.Confirmed, trail.FromStatus);
        Assert.Equal(BadgeProgressStatus.Rejected, trail.ToStatus);
        Assert.Equal("RemovedConfirmed", trail.Action);
        Assert.Equal("remove", trail.Note);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotifyMemberOwnerWithWarn_WhenConfirmedProgressIsRemoved()
    {
        // Arrange
        var ownerUserKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();
        var badgeId = "badge-review-removed";
        var progress = CreateProgress(memberKey, badgeId, BadgeProgressStatus.Confirmed);

        _badgeProgressRepositoryMock
            .Setup(x => x.GetByMemberAndBadgeIdAsync(memberKey, badgeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);
        _memberDirectoryMock
            .Setup(x => x.FindAccountKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownerUserKey);

        var request = new ReviewBadgeProgressCommand(memberKey, badgeId, isApproved: false, note: "remove");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        _eventsMock.Verify(x => x.PublishAsync(
            It.Is<BadgeProgressReviewed>(raised =>
                raised.MemberUserKey == ownerUserKey
                && !raised.IsApproved
                && raised.ConfirmationWithdrawn),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSkipNotification_WhenMemberHasNoLinkedUser()
    {
        // Arrange
        var memberKey = Guid.NewGuid();
        var badgeId = "badge-no-user";
        var progress = CreateProgress(memberKey, badgeId, BadgeProgressStatus.Submitted);

        _badgeProgressRepositoryMock
            .Setup(x => x.GetByMemberAndBadgeIdAsync(memberKey, badgeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);
        _memberDirectoryMock
            .Setup(x => x.FindAccountKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var request = new ReviewBadgeProgressCommand(memberKey, badgeId, isApproved: false, note: "reject");

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        _eventsMock.Verify(x => x.PublishAsync(
            It.IsAny<BadgeProgressReviewed>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenTryingToApproveAlreadyConfirmedProgress()
    {
        // Arrange
        var memberKey = Guid.NewGuid();
        var badgeId = "badge-3";
        var progress = CreateProgress(memberKey, badgeId, BadgeProgressStatus.Confirmed);

        _badgeProgressRepositoryMock
            .Setup(x => x.GetByMemberAndBadgeIdAsync(memberKey, badgeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);

        var request = new ReviewBadgeProgressCommand(memberKey, badgeId, isApproved: true, note: null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Conflict, result.Type);
        Assert.Equal(BadgeProgressStatus.Confirmed, progress.Status);
        Assert.Empty(_journal.Entries);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static BadgeProgress CreateProgress(Guid memberKey, string badgeId, BadgeProgressStatus status)
    {
        return new BadgeProgress
        {
            BadgeProgressKey = Guid.NewGuid(),
            MemberKey = memberKey,
            KurinKey = Guid.NewGuid(),
            BadgeId = badgeId,
            Status = status,
            SubmittedAtUtc = DateTime.UtcNow.AddDays(-1)
        };
    }
}
