using Moq;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe.UpdateStatus;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
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
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.ProbesAndBadgesModule.HandlerTests;

public class UpdateProbeProgressStatusHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMemberDirectory> _memberDirectoryMock;
    private readonly Mock<IProbeProgressRepository> _probeProgressRepositoryMock;
    private readonly Mock<IProbePointProgressRepository> _probePointProgressRepositoryMock;
    private readonly Mock<IProbesCatalogService> _probesCatalogServiceMock;
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IResourceAccessService> _access = new();
    private readonly Mock<IMemberProbeLine> _line = new();
    private readonly UpdateProbeProgressStatusCommandHandler _handler;
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly JournalRecorder _journal;

    public UpdateProbeProgressStatusHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _memberDirectoryMock = new Mock<IMemberDirectory>();
        _probeProgressRepositoryMock = new Mock<IProbeProgressRepository>();
        _probePointProgressRepositoryMock = new Mock<IProbePointProgressRepository>();
        _probesCatalogServiceMock = new Mock<IProbesCatalogService>();
        _line.Setup(x => x.CanStartAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProbeStart.Allowed);
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _access.Setup(x => x.CheckCapabilityAsync(It.IsAny<KurinCapability>(), It.IsAny<ResourceType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());
        _unitOfWorkMock.SetupGet(x => x.ProbeProgresses).Returns(_probeProgressRepositoryMock.Object);
        _unitOfWorkMock.SetupGet(x => x.ProbePointProgresses).Returns(_probePointProgressRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _probePointProgressRepositoryMock
            .Setup(x => x.GetByMemberAndProbeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _probesCatalogServiceMock
            .Setup(x => x.GetGroupedProbeById(It.IsAny<string>()))
            .Returns((GroupedProbeResponse?)null);

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

        _handler = new UpdateProbeProgressStatusCommandHandler(
            _unitOfWorkMock.Object,
            _memberDirectoryMock.Object,
            _journal.Actors,
            _journal.Audit,
            _journal.Reader,
            _probesCatalogServiceMock.Object,
            _access.Object,
            _line.Object);
    }

    [Fact]
    public async Task Handle_ShouldRefuseToStartAProbe_ThatIsNotInTheMembersLine()
    {
        var memberKey = Guid.NewGuid();
        _memberDirectoryMock
            .Setup(x => x.FindKurinKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_kurinKey);
        _line.Setup(x => x.CanStartAsync(memberKey, "probe-1-2026", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProbeStart.NotInLine);

        var result = await _handler.Handle(
            new UpdateProbeProgressStatusCommand(memberKey, "probe-1-2026", ProbeProgressStatus.InProgress, null), CancellationToken.None);

        Assert.Equal(ResultType.Conflict, result.Type);
        Assert.Equal("ProbeNotInLine", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenCompletedProbeIsReopenedViaStatusEndpoint()
    {
        // Arrange
        var memberKey = Guid.NewGuid();
        var actorUserKey = _currentUserContextMock.Object.UserId!.Value;

        var request = new UpdateProbeProgressStatusCommand(memberKey, "probe-1", ProbeProgressStatus.InProgress, "unsign");

        _memberDirectoryMock
            .Setup(x => x.FindKurinKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _memberDirectoryMock
            .Setup(x => x.FindByAccountAsync(actorUserKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemberSummary?)null);

        var progress = new ProbeProgress
        {
            MemberKey = memberKey,
            KurinKey = Guid.NewGuid(),
            ProbeId = "probe-1",
            Status = ProbeProgressStatus.Completed,
            CompletedAtUtc = DateTime.UtcNow.AddDays(-1),
            CompletedByUserKey = actorUserKey,
            CompletedByName = "Some Signer",
            CompletedByRole = "Mentor",
            VerifiedAtUtc = DateTime.UtcNow,
            VerifiedByUserKey = actorUserKey,
            VerifiedByName = "Verifier",
            VerifiedByRole = "Manager"
        };

        _probeProgressRepositoryMock
            .Setup(x => x.GetByMemberAndProbeIdAsync(memberKey, "probe-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Conflict, result.Type);
        Assert.Equal(ProbeProgressStatus.Completed, progress.Status);
        Assert.NotNull(progress.CompletedAtUtc);
        Assert.Equal(actorUserKey, progress.CompletedByUserKey);
        Assert.Equal("Some Signer", progress.CompletedByName);
        Assert.Equal("Mentor", progress.CompletedByRole);
        Assert.NotNull(progress.VerifiedAtUtc);
        Assert.Equal(actorUserKey, progress.VerifiedByUserKey);
        Assert.Equal("Verifier", progress.VerifiedByName);
        Assert.Equal("Manager", progress.VerifiedByRole);
        Assert.Empty(_journal.Entries);
        Assert.Empty(result.Data!.AuditTrail);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldUseMemberFullName_WhenActorIsBoundToMember()
    {
        // Arrange
        var memberKey = Guid.NewGuid();
        var actorUserKey = _currentUserContextMock.Object.UserId!.Value;

        var request = new UpdateProbeProgressStatusCommand(memberKey, "probe-1", ProbeProgressStatus.Completed, null);

        _memberDirectoryMock
            .Setup(x => x.FindKurinKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _memberDirectoryMock
            .Setup(x => x.FindByAccountAsync(actorUserKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(
                Guid.NewGuid(), actorUserKey, Guid.Empty, null, "Ivan", "Shevchenko", "ivan@example.com", null));

        var progress = new ProbeProgress
        {
            MemberKey = memberKey,
            KurinKey = Guid.NewGuid(),
            ProbeId = "probe-1",
            Status = ProbeProgressStatus.InProgress
        };

        _probeProgressRepositoryMock
            .Setup(x => x.GetByMemberAndProbeIdAsync(memberKey, "probe-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal("Ivan Shevchenko", progress.CompletedByName);
        Assert.Equal("Mentor", progress.CompletedByRole);

        var trail = Assert.Single(result.Data!.AuditTrail);
        Assert.Equal("Ivan Shevchenko", trail.ActorName);
        Assert.Equal("Mentor", trail.ActorRole);
        Assert.Equal(ProbeProgressStatus.InProgress, trail.FromStatus);
        Assert.Equal(ProbeProgressStatus.Completed, trail.ToStatus);
        Assert.Equal("Completed", trail.Action);

        // The journal names the same actor the signature does, from the one resolution.
        var entry = Assert.Single(_journal.Entries);
        Assert.Equal(AuditActions.Probe.Completed, entry.Action);
        Assert.Equal("Ivan Shevchenko", entry.ActorName);
        Assert.Equal(actorUserKey, entry.ActorUserKey);
        Assert.Equal(progress.ProbeProgressKey, entry.SubjectKey);
        Assert.Equal(new AuditChange("status", "InProgress", "Completed"), Assert.Single(entry.Changes));
        Assert.Equal(entry.KurinAuditEventKey, trail.ProbeProgressAuditEventKey);
        Assert.Equal([progress.ProbeProgressKey], Assert.Single(_journal.SubjectReads));
        _memberDirectoryMock.Verify(x => x.FindByAccountAsync(actorUserKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenClosingWithoutAllSignedPoints()
    {
        // Arrange
        var memberKey = Guid.NewGuid();
        var actorUserKey = _currentUserContextMock.Object.UserId!.Value;

        var request = new UpdateProbeProgressStatusCommand(memberKey, "probe-1", ProbeProgressStatus.Completed, null);

        _memberDirectoryMock
            .Setup(x => x.FindKurinKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _memberDirectoryMock
            .Setup(x => x.FindByAccountAsync(actorUserKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemberSummary?)null);

        _probesCatalogServiceMock
            .Setup(x => x.GetGroupedProbeById("probe-1"))
            .Returns(new GroupedProbeResponse("probe-1", "Перша проба", 2, 1, []));

        _probePointProgressRepositoryMock
            .Setup(x => x.GetByMemberAndProbeAsync(memberKey, "probe-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ProbePointProgress
                {
                    MemberKey = memberKey,
                    KurinKey = Guid.NewGuid(),
                    ProbeId = "probe-1",
                    PointId = "point-1",
                    IsSigned = true,
                    SignedAtUtc = DateTime.UtcNow,
                    SignedByUserKey = actorUserKey,
                    SignedByName = "Mentor"
                }
            ]);

        var progress = new ProbeProgress
        {
            MemberKey = memberKey,
            KurinKey = Guid.NewGuid(),
            ProbeId = "probe-1",
            Status = ProbeProgressStatus.InProgress
        };

        _probeProgressRepositoryMock
            .Setup(x => x.GetByMemberAndProbeIdAsync(memberKey, "probe-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Conflict, result.Type);
        Assert.Equal(ProbeProgressStatus.InProgress, progress.Status);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true, ResultType.Success, ProbeProgressStatus.Completed)]
    [InlineData(false, ResultType.Forbidden, ProbeProgressStatus.Verified)]
    public async Task Handle_TakingAProbeBackFromVerified_TakesUnsignProbe(bool mayUnsign, ResultType expected, ProbeProgressStatus after)
    {
        var memberKey = Guid.NewGuid();
        _access.Setup(x => x.CheckCapabilityAsync(KurinCapability.UnsignProbe, ResourceType.Member, memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mayUnsign ? ResourceAccessDecision.Allow() : ResourceAccessDecision.Deny("Refused by the test."));
        _memberDirectoryMock
            .Setup(x => x.FindKurinKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        var progress = new ProbeProgress
        {
            MemberKey = memberKey,
            ProbeId = "probe-1",
            Status = ProbeProgressStatus.Verified,
            VerifiedAtUtc = DateTime.UtcNow
        };
        _probeProgressRepositoryMock
            .Setup(x => x.GetByMemberAndProbeIdAsync(memberKey, "probe-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);

        var result = await _handler.Handle(
            new UpdateProbeProgressStatusCommand(memberKey, "probe-1", ProbeProgressStatus.Completed, null),
            CancellationToken.None);

        Assert.Equal(expected, result.Type);
        Assert.Equal(after, progress.Status);
        Assert.Equal(
            mayUnsign ? new[] { AuditActions.Probe.Unverified } : Array.Empty<string>(),
            _journal.Entries.Select(entry => entry.Action).ToArray());
    }
}
