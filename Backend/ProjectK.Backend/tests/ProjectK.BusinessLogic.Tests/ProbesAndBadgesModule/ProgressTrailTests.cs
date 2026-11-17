using Moq;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AuditModule;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Tests.ProbesAndBadgesModule;

/// <summary>
/// The status history on a проба or a вмілість comes from the kurin's journal now, but the clients
/// still read it as it was: the same action names, the same statuses, oldest first — and a list
/// asks the journal once, however long it is.
/// </summary>
public class ProgressTrailTests
{
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _memberKey = Guid.NewGuid();
    private readonly DateTime _start = new(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);

    private KurinAuditEvent Entry(Guid subjectKey, string action, string? from, string? to, int minutes, string? note = null) => new()
    {
        KurinKey = _kurinKey,
        OccurredAtUtc = _start.AddMinutes(minutes),
        ActorUserKey = Guid.NewGuid(),
        ActorName = "Марта",
        ActorRole = "KV:Vykhovnyk",
        Area = action.StartsWith("Probe.", StringComparison.Ordinal) ? AuditArea.Probe : AuditArea.Badge,
        Action = action,
        SubjectMemberKey = _memberKey,
        SubjectKey = subjectKey,
        SubjectLabel = "label",
        Changes = from is null && to is null ? [] : [new AuditChange("status", from, to)],
        Note = note
    };

    [Theory]
    [InlineData(AuditActionNames.Probe.Started, "NotStarted", "InProgress", "InProgress", ProbeProgressStatus.NotStarted, ProbeProgressStatus.InProgress)]
    [InlineData(AuditActionNames.Probe.Completed, "InProgress", "Completed", "Completed", ProbeProgressStatus.InProgress, ProbeProgressStatus.Completed)]
    [InlineData(AuditActionNames.Probe.Verified, "Completed", "Verified", "Verified", ProbeProgressStatus.Completed, ProbeProgressStatus.Verified)]
    [InlineData(AuditActionNames.Probe.Unverified, "Verified", "Completed", "Unverify", ProbeProgressStatus.Verified, ProbeProgressStatus.Completed)]
    [InlineData(AuditActionNames.Probe.Reopened, "Completed", "InProgress", "Unsign", ProbeProgressStatus.Completed, ProbeProgressStatus.InProgress)]
    public void ForProbe_ShouldReadAJournalStatusChangeAsTheOldTrailItem(
        string action, string from, string to, string oldAction, ProbeProgressStatus fromStatus, ProbeProgressStatus toStatus)
    {
        var progressKey = Guid.NewGuid();
        var entry = Entry(progressKey, action, from, to, 0, "так");

        var item = Assert.Single(ProgressTrail.ForProbe(progressKey, [entry]));

        Assert.Equal(entry.KurinAuditEventKey, item.ProbeProgressAuditEventKey);
        Assert.Equal(fromStatus, item.FromStatus);
        Assert.Equal(toStatus, item.ToStatus);
        Assert.Equal(oldAction, item.Action);
        Assert.Equal(entry.ActorUserKey, item.ActorUserKey);
        Assert.Equal("Марта", item.ActorName);
        Assert.Equal("KV:Vykhovnyk", item.ActorRole);
        Assert.Equal(entry.OccurredAtUtc, item.OccurredAtUtc);
        Assert.Equal("так", item.Note);
    }

    [Theory]
    [InlineData(AuditActionNames.Badge.Submitted, null, "Submitted", "Submitted", null, BadgeProgressStatus.Submitted)]
    [InlineData(AuditActionNames.Badge.Resubmitted, "Rejected", "Submitted", "Resubmitted", BadgeProgressStatus.Rejected, BadgeProgressStatus.Submitted)]
    [InlineData(AuditActionNames.Badge.Confirmed, "Submitted", "Confirmed", "Confirmed", BadgeProgressStatus.Submitted, BadgeProgressStatus.Confirmed)]
    [InlineData(AuditActionNames.Badge.Rejected, "Submitted", "Rejected", "Rejected", BadgeProgressStatus.Submitted, BadgeProgressStatus.Rejected)]
    [InlineData(AuditActionNames.Badge.ConfirmationRemoved, "Confirmed", "Rejected", "RemovedConfirmed", BadgeProgressStatus.Confirmed, BadgeProgressStatus.Rejected)]
    public void ForBadge_ShouldReadAJournalStatusChangeAsTheOldTrailItem(
        string action, string? from, string to, string oldAction, BadgeProgressStatus? fromStatus, BadgeProgressStatus toStatus)
    {
        var progressKey = Guid.NewGuid();
        var entry = Entry(progressKey, action, from, to, 0);

        var item = Assert.Single(ProgressTrail.ForBadge(progressKey, [entry]));

        Assert.Equal(entry.KurinAuditEventKey, item.BadgeProgressAuditEventKey);
        Assert.Equal(fromStatus, item.FromStatus);
        Assert.Equal(toStatus, item.ToStatus);
        Assert.Equal(oldAction, item.Action);
    }

    [Fact]
    public void ForProbe_ShouldKeepOnlyItsOwnStatusChanges_OldestFirst()
    {
        var progressKey = Guid.NewGuid();
        var journal = new[]
        {
            Entry(progressKey, AuditActionNames.Probe.Verified, "Completed", "Verified", 20),
            Entry(progressKey, AuditActionNames.Probe.PointSigned, null, null, 5),
            Entry(Guid.NewGuid(), AuditActionNames.Probe.Completed, "InProgress", "Completed", 1),
            Entry(progressKey, AuditActionNames.Probe.Completed, "InProgress", "Completed", 10)
        };

        var trail = ProgressTrail.ForProbe(progressKey, journal);

        Assert.Equal(["Completed", "Verified"], trail.Select(item => item.Action));
    }

    [Fact]
    public void ForBadge_WithoutAStatusChange_ShouldFallBackToTheStatusTheActionLeads()
    {
        var progressKey = Guid.NewGuid();

        var item = Assert.Single(ProgressTrail.ForBadge(progressKey, [Entry(progressKey, AuditActionNames.Badge.Confirmed, null, null, 0)]));

        Assert.Null(item.FromStatus);
        Assert.Equal(BadgeProgressStatus.Confirmed, item.ToStatus);
    }

    [Fact]
    public async Task BadgeList_ShouldReadTheJournalOnce_AndGiveEachItsOwnTrail()
    {
        var user = new Mock<ICurrentUserContext>();
        var journal = new JournalRecorder(user.Object);
        var first = new BadgeProgress { MemberKey = _memberKey, KurinKey = _kurinKey, BadgeId = "a", Status = BadgeProgressStatus.Confirmed };
        var second = new BadgeProgress { MemberKey = _memberKey, KurinKey = _kurinKey, BadgeId = "b", Status = BadgeProgressStatus.Submitted };
        var third = new BadgeProgress { MemberKey = _memberKey, KurinKey = _kurinKey, BadgeId = "c", Status = BadgeProgressStatus.Draft };
        journal.Entries.AddRange(
        [
            Entry(first.BadgeProgressKey, AuditActionNames.Badge.Submitted, null, "Submitted", 0),
            Entry(first.BadgeProgressKey, AuditActionNames.Badge.Confirmed, "Submitted", "Confirmed", 1),
            Entry(second.BadgeProgressKey, AuditActionNames.Badge.Submitted, null, "Submitted", 2)
        ]);

        var progresses = new Mock<IBadgeProgressRepository>();
        progresses.Setup(x => x.GetByMemberKeyAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync([first, second, third]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.BadgeProgresses).Returns(progresses.Object);
        var members = new Mock<IMemberDirectory>();
        members.Setup(x => x.ExistsAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await new GetBadgeProgressesQueryHandler(unitOfWork.Object, members.Object, journal.Reader)
            .Handle(new GetBadgeProgressesQuery(_memberKey), CancellationToken.None);

        var read = Assert.Single(journal.SubjectReads);
        Assert.Equal(new[] { first.BadgeProgressKey, second.BadgeProgressKey, third.BadgeProgressKey }.Order(), read.Order());

        var byKey = result.Data!.ToDictionary(item => item.BadgeProgressKey);
        Assert.Equal(["Submitted", "Confirmed"], byKey[first.BadgeProgressKey].AuditTrail.Select(item => item.Action));
        Assert.Equal(["Submitted"], byKey[second.BadgeProgressKey].AuditTrail.Select(item => item.Action));
        Assert.Empty(byKey[third.BadgeProgressKey].AuditTrail);
    }

    [Fact]
    public async Task ReviewQueue_ShouldReadTheJournalOnce_ForTheSubmittedOnly()
    {
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        var journal = new JournalRecorder(user.Object);
        var otherMember = Guid.NewGuid();
        var waiting = new BadgeProgress { MemberKey = _memberKey, KurinKey = _kurinKey, BadgeId = "a", Status = BadgeProgressStatus.Submitted };
        var alsoWaiting = new BadgeProgress { MemberKey = otherMember, KurinKey = _kurinKey, BadgeId = "b", Status = BadgeProgressStatus.Submitted };
        var done = new BadgeProgress { MemberKey = _memberKey, KurinKey = _kurinKey, BadgeId = "c", Status = BadgeProgressStatus.Confirmed };
        journal.Entries.Add(Entry(waiting.BadgeProgressKey, AuditActionNames.Badge.Submitted, null, "Submitted", 0, "прошу"));

        var members = new Mock<IMemberDirectory>();
        members.Setup(x => x.GetByKurinAsync(_kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new MemberSummary(_memberKey, null, _kurinKey, null, "Олесь", "Юнак", "", null),
            new MemberSummary(otherMember, null, _kurinKey, null, "Петро", "Юнак", "", null)
        ]);
        var progresses = new Mock<IBadgeProgressRepository>();
        progresses.Setup(x => x.GetByMemberKeysAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([waiting, alsoWaiting, done]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.BadgeProgresses).Returns(progresses.Object);
        var permissions = new Mock<ICurrentPermissions>();
        permissions.Setup(x => x.ReachAsync(It.IsAny<KurinCapability>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CapabilityReach.Kurin);

        var result = await new GetBadgeReviewQueueQueryHandler(
                UnitOfWorks.WithScopes(Mock.Of<IResourceScopeRepository>(), unitOfWork), members.Object, user.Object, FakeBlobReadLinks.Instance,
                permissions.Object, journal.Reader, Mock.Of<IResourceScopeReader>())
            .Handle(new GetBadgeReviewQueueQuery(_kurinKey), CancellationToken.None);

        var read = Assert.Single(journal.SubjectReads);
        Assert.Equal(new[] { waiting.BadgeProgressKey, alsoWaiting.BadgeProgressKey }.Order(), read.Order());
        var item = Assert.Single(result.Data!, response => response.BadgeProgressKey == waiting.BadgeProgressKey);
        Assert.Equal("прошу", Assert.Single(item.AuditTrail).Note);
    }

    [Fact]
    public async Task Probe_ShouldCarryItsTrailFromTheJournal()
    {
        var user = new Mock<ICurrentUserContext>();
        var journal = new JournalRecorder(user.Object);
        var progress = new ProbeProgress { MemberKey = _memberKey, KurinKey = _kurinKey, ProbeId = "probe-1", Status = ProbeProgressStatus.Verified };
        journal.Entries.AddRange(
        [
            Entry(progress.ProbeProgressKey, AuditActionNames.Probe.PointSigned, null, null, 0),
            Entry(progress.ProbeProgressKey, AuditActionNames.Probe.Completed, "InProgress", "Completed", 1),
            Entry(progress.ProbeProgressKey, AuditActionNames.Probe.Verified, "Completed", "Verified", 2)
        ]);

        var probes = new Mock<IProbeProgressRepository>();
        probes.Setup(x => x.GetByMemberAndProbeIdAsync(_memberKey, "probe-1", It.IsAny<CancellationToken>())).ReturnsAsync(progress);
        var points = new Mock<IProbePointProgressRepository>();
        points.Setup(x => x.GetByMemberAndProbeAsync(_memberKey, "probe-1", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.ProbeProgresses).Returns(probes.Object);
        unitOfWork.SetupGet(x => x.ProbePointProgresses).Returns(points.Object);
        var members = new Mock<IMemberDirectory>();
        members.Setup(x => x.FindKurinKeyAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(_kurinKey);

        var result = await new GetProbeProgressQueryHandler(unitOfWork.Object, members.Object, journal.Reader)
            .Handle(new GetProbeProgressQuery(_memberKey, "probe-1"), CancellationToken.None);

        Assert.Equal([progress.ProbeProgressKey], Assert.Single(journal.SubjectReads));
        Assert.Equal(["Completed", "Verified"], result.Data!.AuditTrail.Select(item => item.Action));
    }
}
