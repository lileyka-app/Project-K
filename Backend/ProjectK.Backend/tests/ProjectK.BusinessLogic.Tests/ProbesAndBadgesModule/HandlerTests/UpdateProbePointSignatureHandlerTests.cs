using Moq;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe.UpdatePointSignature;
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
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.ProbesAndBadgesModule.HandlerTests;

public class UpdateProbePointSignatureHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMemberDirectory> _memberDirectoryMock;
    private readonly Mock<IProbeProgressRepository> _probeProgressRepositoryMock;
    private readonly Mock<IProbePointProgressRepository> _probePointProgressRepositoryMock;
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly UpdateProbePointSignatureCommandHandler _handler;
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly JournalRecorder _journal;

    public UpdateProbePointSignatureHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _memberDirectoryMock = new Mock<IMemberDirectory>();
        _probeProgressRepositoryMock = new Mock<IProbeProgressRepository>();
        _probePointProgressRepositoryMock = new Mock<IProbePointProgressRepository>();
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _unitOfWorkMock.SetupGet(x => x.ProbeProgresses).Returns(_probeProgressRepositoryMock.Object);
        _unitOfWorkMock.SetupGet(x => x.ProbePointProgresses).Returns(_probePointProgressRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _currentUserContextMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _currentUserContextMock.Setup(x => x.IsInRole("Admin")).Returns(false);
        _currentUserContextMock.Setup(x => x.IsInRole("Manager")).Returns(false);
        _currentUserContextMock.Setup(x => x.IsInRole("Mentor")).Returns(true);
        _currentUserContextMock.Setup(x => x.IsInRole("User")).Returns(false);
        _currentUserContextMock.SetupGet(x => x.Roles).Returns(new[] { "Mentor" });

        _currentUserContextMock.SetupGet(x => x.KurinKey).Returns(_kurinKey);

        _journal = new JournalRecorder(
            _currentUserContextMock.Object,
            new AuditActorResolver(_currentUserContextMock.Object, _memberDirectoryMock.Object));

        _handler = new UpdateProbePointSignatureCommandHandler(
            _unitOfWorkMock.Object,
            _memberDirectoryMock.Object,
            _journal.Actors,
            _journal.Audit,
            _journal.Reader,
            Mock.Of<IProbesCatalogService>());
    }

    [Fact]
    public async Task Signing_ShouldRecordThePointInTheJournal_WithTheSubjectsGroup()
    {
        var memberKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        _journal.Knows(memberKey, _kurinKey, groupKey, "Олесь", "Юнак");
        _memberDirectoryMock
            .Setup(x => x.FindKurinKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_kurinKey);
        _memberDirectoryMock
            .Setup(x => x.FindByAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(Guid.NewGuid(), _currentUserContextMock.Object.UserId, _kurinKey, null, "Марта", "Впорядниця", "m@example.com", null));
        _probePointProgressRepositoryMock
            .Setup(x => x.GetByMemberAndProbeAsync(memberKey, "probe-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _handler.Handle(
            new UpdateProbePointSignatureCommand(memberKey, "probe-1", "point-1", true, "на сходинах"),
            CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        var entry = Assert.Single(_journal.Entries);
        Assert.Equal(AuditActions.Probe.PointSigned, entry.Action);
        Assert.Equal(AuditArea.Probe, entry.Area);
        Assert.Equal(_kurinKey, entry.KurinKey);
        Assert.Equal("Марта Впорядниця", entry.ActorName);
        Assert.Equal("Mentor", entry.ActorRole);
        Assert.Equal(memberKey, entry.SubjectMemberKey);
        Assert.Equal("Олесь Юнак", entry.SubjectMemberName);
        Assert.Equal(groupKey, entry.SubjectGroupKey);
        Assert.Equal("probe-1", entry.SubjectLabel);
        Assert.Equal("на сходинах", entry.Note);
        var change = Assert.Single(entry.Changes);
        Assert.Equal(new AuditChange("point", null, "point-1"), change);
    }

    [Fact]
    public async Task Handle_ShouldNotDowngradeClosedProbe_WhenUnsignDidNotChangePoint()
    {
        // Arrange
        var memberKey = Guid.NewGuid();
        var request = new UpdateProbePointSignatureCommand(memberKey, "probe-1", "point-1", false, null);

        _memberDirectoryMock
            .Setup(x => x.FindKurinKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _memberDirectoryMock
            .Setup(x => x.FindByAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemberSummary?)null);

        _probePointProgressRepositoryMock
            .Setup(x => x.GetByMemberProbePointAsync(memberKey, "probe-1", "point-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProbePointProgress?)null);

        var progress = new ProbeProgress
        {
            MemberKey = memberKey,
            KurinKey = Guid.NewGuid(),
            ProbeId = "probe-1",
            Status = ProbeProgressStatus.Completed,
            CompletedAtUtc = DateTime.UtcNow.AddHours(-1),
            CompletedByUserKey = Guid.NewGuid(),
            CompletedByName = "Mentor",
            CompletedByRole = "Mentor"
        };

        _probeProgressRepositoryMock
            .Setup(x => x.GetByMemberAndProbeIdAsync(memberKey, "probe-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);

        _probePointProgressRepositoryMock
            .Setup(x => x.GetByMemberAndProbeAsync(memberKey, "probe-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(ProbeProgressStatus.Completed, progress.Status);
        Assert.NotNull(progress.CompletedAtUtc);
        Assert.NotNull(progress.CompletedByUserKey);
        Assert.NotNull(progress.CompletedByName);
        Assert.NotNull(progress.CompletedByRole);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(_journal.Entries);
    }

    [Fact]
    public async Task Handle_ShouldDowngradeClosedProbe_WhenSignedPointIsUnSigned()
    {
        // Arrange
        var memberKey = Guid.NewGuid();
        var actorUserKey = _currentUserContextMock.Object.UserId!.Value;
        var request = new UpdateProbePointSignatureCommand(memberKey, "probe-1", "point-1", false, null);

        _memberDirectoryMock
            .Setup(x => x.FindKurinKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _memberDirectoryMock
            .Setup(x => x.FindByAccountAsync(actorUserKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemberSummary?)null);

        var pointProgress = new ProbePointProgress
        {
            MemberKey = memberKey,
            KurinKey = Guid.NewGuid(),
            ProbeId = "probe-1",
            PointId = "point-1",
            IsSigned = true,
            SignedAtUtc = DateTime.UtcNow,
            SignedByUserKey = actorUserKey,
            SignedByName = "Mentor",
            SignedByRole = "Mentor"
        };

        _probePointProgressRepositoryMock
            .Setup(x => x.GetByMemberProbePointAsync(memberKey, "probe-1", "point-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pointProgress);

        var progress = new ProbeProgress
        {
            MemberKey = memberKey,
            KurinKey = Guid.NewGuid(),
            ProbeId = "probe-1",
            Status = ProbeProgressStatus.Completed,
            CompletedAtUtc = DateTime.UtcNow.AddHours(-1),
            CompletedByUserKey = actorUserKey,
            CompletedByName = "Mentor",
            CompletedByRole = "Mentor"
        };

        _probeProgressRepositoryMock
            .Setup(x => x.GetByMemberAndProbeIdAsync(memberKey, "probe-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);

        _probePointProgressRepositoryMock
            .Setup(x => x.GetByMemberAndProbeAsync(memberKey, "probe-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([pointProgress]);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.False(pointProgress.IsSigned);
        Assert.Null(pointProgress.SignedAtUtc);
        Assert.Null(pointProgress.SignedByUserKey);
        Assert.Null(pointProgress.SignedByName);
        Assert.Null(pointProgress.SignedByRole);

        Assert.Equal(ProbeProgressStatus.InProgress, progress.Status);
        Assert.Null(progress.CompletedAtUtc);
        Assert.Null(progress.CompletedByUserKey);
        Assert.Null(progress.CompletedByName);
        Assert.Null(progress.CompletedByRole);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // The unsign silently sent the проба back to work; the journal says so as its own entry.
        Assert.Collection(
            _journal.Entries,
            unsigned =>
            {
                Assert.Equal(AuditActions.Probe.PointUnsigned, unsigned.Action);
                Assert.Equal(new AuditChange("point", "point-1", null), Assert.Single(unsigned.Changes));
                Assert.Equal(progress.ProbeProgressKey, unsigned.SubjectKey);
            },
            reopened =>
            {
                Assert.Equal(AuditActions.Probe.Reopened, reopened.Action);
                Assert.Equal(new AuditChange("status", "Completed", "InProgress"), Assert.Single(reopened.Changes));
                Assert.Equal(progress.ProbeProgressKey, reopened.SubjectKey);
            });
    }
}
