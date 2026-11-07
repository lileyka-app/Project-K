using FluentAssertions;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Archive.Correct;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Archive.DangerZone;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Archive.Delete;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.ArchiveHandlers;

/// <summary>
/// The archive's rules: corrections stay within reach and off one's own record; deletions need the
/// right, a live danger zone of one's own in this kurin, and every one of them reaches the journal.
/// </summary>
public class KurinArchiveHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _userKey = Guid.NewGuid();
    private readonly Guid _selfMemberKey = Guid.NewGuid();
    private readonly Guid _ledGroup = Guid.NewGuid();
    private const string Stamp = "stamp-1";

    private readonly FixedTimeProvider _time = new(Now);
    private readonly Mock<ICurrentUserContext> _user = new();
    private readonly Mock<ICurrentPermissions> _permissions = new();
    private readonly Mock<IResourceScopeReader> _scopes = new();
    private readonly Mock<IMemberDirectory> _members = new();
    private readonly Mock<IJwtService> _jwt = new();
    private readonly Mock<IReauthentication> _reauth = new();
    private readonly Mock<IKurinAudit> _audit = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IKurinArchiveRepository> _archive = new();
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly ArchiveDangerZoneRegistry _registry = new();
    private readonly List<AuditRecord> _journal = [];

    public KurinArchiveHandlerTests()
    {
        _user.Setup(u => u.UserId).Returns(_userKey);
        _user.Setup(u => u.KurinKey).Returns(_kurinKey);
        _members.Setup(m => m.FindByAccountAsync(_userKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(_selfMemberKey, _userKey, _kurinKey, null, "Ірина", "Звʼязкова", "z@example.com", null));
        _members.Setup(m => m.GetByKeysAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> keys, CancellationToken _) =>
                keys.Select(key => new MemberSummary(key, null, _kurinKey, null, "Марко", "Бойко", "m@example.com", null)).ToList());
        _scopes.Setup(s => s.GetLedGroupKeysAsync(_userKey, _kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync([_ledGroup]);
        _reauth.Setup(r => r.GetSecurityStampAsync(_userKey, It.IsAny<CancellationToken>())).ReturnsAsync(Stamp);
        _jwt.Setup(j => j.GenerateArchiveDangerTicket(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns((Guid user, Guid kurin, Guid ticket, string stamp, DateTime _) => $"{user}|{kurin}|{ticket}|{stamp}");
        _jwt.Setup(j => j.ReadArchiveDangerTicket(It.IsAny<string>()))
            .Returns((string token) =>
            {
                var parts = token.Split('|');
                return parts.Length == 4 ? new ArchiveDangerTicket(Guid.Parse(parts[0]), Guid.Parse(parts[1]), Guid.Parse(parts[2]), parts[3]) : null;
            });
        _audit.Setup(a => a.RecordAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()))
            .Callback((AuditRecord record, CancellationToken _) => _journal.Add(record))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.Archive).Returns(_archive.Object);
        _unitOfWork.Setup(u => u.Kurins).Returns(_kurins.Object);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        Grant(AccessScope.KurinWide, ResourceAction.Read, ResourceAction.Update, ResourceAction.Delete);
    }

    private void Grant(AccessScope scope, params ResourceAction[] actions)
    {
        _permissions.Reset();
        foreach (var action in Enum.GetValues<ResourceAction>())
        {
            _permissions.Setup(p => p.WidestScopeAsync(ResourceType.KurinArchive, action, It.IsAny<CancellationToken>()))
                .ReturnsAsync(actions.Contains(action) ? scope : null);
        }
    }

    private ArchiveReach Reach() => new(_user.Object, _permissions.Object, _scopes.Object, _members.Object);
    private ArchiveDangerZone Zone() => new(_registry, _jwt.Object, _reauth.Object, _user.Object, _time);
    private ArchiveDeletionGate Gate() => new(Reach(), Zone(), _user.Object, _audit.Object);

    private string OpenZone() => Zone().Open(_userKey, _kurinKey, Stamp).Ticket;

    private LeadershipHistory FormerOffice(Guid? memberKey = null, Guid? groupKey = null, DateOnly? end = null) => new()
    {
        LeadershipHistoryKey = Guid.NewGuid(),
        MemberKey = memberKey ?? Guid.NewGuid(),
        Role = LeadershipRole.Pysar,
        StartDate = new DateOnly(2021, 9, 1),
        EndDate = end ?? new DateOnly(2023, 8, 31),
        Leadership = groupKey is null
            ? new Leadership { Type = LeadershipType.Kurin, KurinKey = _kurinKey }
            : new Leadership { Type = LeadershipType.Group, GroupKey = groupKey, Group = new Group("Соколи", _kurinKey) { GroupKey = groupKey.Value } }
    };

    private DeleteArchivedOfficeCommandHandler DeleteOffice() => new(_unitOfWork.Object, _members.Object, Gate(), _audit.Object);

    [Fact]
    public async Task Deleting_WithoutTheZone_IsRefused()
    {
        var office = FormerOffice();
        _archive.Setup(a => a.FindOfficeAsync(office.LeadershipHistoryKey, It.IsAny<CancellationToken>())).ReturnsAsync(office);

        var result = await DeleteOffice().Handle(new DeleteArchivedOfficeCommand(_kurinKey, office.LeadershipHistoryKey, null), default);

        result.Type.Should().Be(ResultType.Forbidden);
        result.ErrorCode.Should().Be(ArchiveErrors.DangerZoneClosed);
        _archive.Verify(a => a.RemoveOffice(It.IsAny<LeadershipHistory>()), Times.Never);
    }

    [Fact]
    public async Task Deleting_InTheZone_RemovesTheRecord_AndTheJournalKeepsWhatItWas()
    {
        var office = FormerOffice();
        _archive.Setup(a => a.FindOfficeAsync(office.LeadershipHistoryKey, It.IsAny<CancellationToken>())).ReturnsAsync(office);

        var result = await DeleteOffice().Handle(new DeleteArchivedOfficeCommand(_kurinKey, office.LeadershipHistoryKey, OpenZone()), default);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.DeletionsLeft.Should().Be(ArchiveDangerZone.Deletions - 1);
        _archive.Verify(a => a.RemoveOffice(office), Times.Once);
        var entry = _journal.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditActions.Archive.OfficeDeleted);
        entry.Changes!.Select(change => change.Field).Should().Contain(["holder", "role", "startDate", "endDate"]);
        entry.Changes!.Should().OnlyContain(change => change.To == null);
    }

    [Fact]
    public async Task NobodyDeletesTheirOwnRecord_EvenInTheZone()
    {
        var office = FormerOffice(_selfMemberKey);
        _archive.Setup(a => a.FindOfficeAsync(office.LeadershipHistoryKey, It.IsAny<CancellationToken>())).ReturnsAsync(office);

        var result = await DeleteOffice().Handle(new DeleteArchivedOfficeCommand(_kurinKey, office.LeadershipHistoryKey, OpenZone()), default);

        result.ErrorCode.Should().Be(ArchiveErrors.OwnRecord);
        _registry.Find(_registry.Close(_userKey, _kurinKey)!.TicketId).Should().BeNull();
    }

    [Fact]
    public async Task AGroupScopedLeader_DeletesNothing()
    {
        Grant(AccessScope.OwnGroups, ResourceAction.Read, ResourceAction.Update, ResourceAction.Delete);
        var office = FormerOffice(groupKey: _ledGroup);

        var result = await DeleteOffice().Handle(new DeleteArchivedOfficeCommand(_kurinKey, office.LeadershipHistoryKey, OpenZone()), default);

        result.ErrorCode.Should().Be(ArchiveErrors.OutOfReach);
    }

    [Fact]
    public async Task TheZone_DoesNotCarryToAnotherKurin_OrPastAPasswordChange_OrPastItsEnd()
    {
        var ticket = OpenZone();
        var zone = Zone();
        (await zone.CheckAsync(ticket, _kurinKey, default)).Should().NotBeNull();

        (await zone.CheckAsync(ticket, Guid.NewGuid(), default)).Should().BeNull();

        _reauth.Setup(r => r.GetSecurityStampAsync(_userKey, It.IsAny<CancellationToken>())).ReturnsAsync("stamp-2");
        (await zone.CheckAsync(ticket, _kurinKey, default)).Should().BeNull();

        _reauth.Setup(r => r.GetSecurityStampAsync(_userKey, It.IsAny<CancellationToken>())).ReturnsAsync(Stamp);
        _time.Advance(ArchiveDangerZone.Lifetime);
        (await zone.CheckAsync(ticket, _kurinKey, default)).Should().BeNull();
        zone.Close(_userKey, _kurinKey).Should().BeTrue("a zone that ran out of time is journalled as such");
    }

    [Fact]
    public async Task TheTenthDeletion_EndsTheZone()
    {
        var ticket = OpenZone();
        for (var i = 0; i < ArchiveDangerZone.Deletions; i++)
        {
            var office = FormerOffice();
            _archive.Setup(a => a.FindOfficeAsync(office.LeadershipHistoryKey, It.IsAny<CancellationToken>())).ReturnsAsync(office);
            (await DeleteOffice().Handle(new DeleteArchivedOfficeCommand(_kurinKey, office.LeadershipHistoryKey, ticket), default))
                .Type.Should().Be(ResultType.Success);
        }

        var eleventh = FormerOffice();
        _archive.Setup(a => a.FindOfficeAsync(eleventh.LeadershipHistoryKey, It.IsAny<CancellationToken>())).ReturnsAsync(eleventh);
        var result = await DeleteOffice().Handle(new DeleteArchivedOfficeCommand(_kurinKey, eleventh.LeadershipHistoryKey, ticket), default);

        result.ErrorCode.Should().Be(ArchiveErrors.DangerZoneClosed);
        _journal.Should().ContainSingle(entry => entry.Action == AuditActions.Archive.DangerZoneEnded);
    }

    [Fact]
    public async Task AWrongPassword_IsJournalled_AndOpensNothing()
    {
        _reauth.Setup(r => r.VerifyAsync(_userKey, "nope", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReauthenticationResult(ReauthenticationOutcome.Wrong, ReauthenticationMethod.Password));
        var events = new Mock<IDomainEventPublisher>();
        var handler = new EnterArchiveDangerZoneCommandHandler(_unitOfWork.Object, Reach(), Zone(), _reauth.Object, _user.Object, _members.Object, _audit.Object, events.Object);

        var result = await handler.Handle(new EnterArchiveDangerZoneCommand(_kurinKey, "nope", null), default);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be(ArchiveErrors.WrongCredentials);
        _journal.Should().ContainSingle(entry => entry.Action == AuditActions.Archive.DangerZoneRefused);
        _registry.Close(_userKey, _kurinKey).Should().BeNull();
        events.Verify(e => e.PublishAsync(It.IsAny<ArchiveDangerZoneEntered>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheRightPassword_OpensTheZone_AndTellsTheLeadership()
    {
        _reauth.Setup(r => r.VerifyAsync(_userKey, "right", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReauthenticationResult(ReauthenticationOutcome.Verified, ReauthenticationMethod.Password, Stamp));
        var events = new Mock<IDomainEventPublisher>();
        var handler = new EnterArchiveDangerZoneCommandHandler(_unitOfWork.Object, Reach(), Zone(), _reauth.Object, _user.Object, _members.Object, _audit.Object, events.Object);

        var result = await handler.Handle(new EnterArchiveDangerZoneCommand(_kurinKey, "right", null), default);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.DeletionsLeft.Should().Be(ArchiveDangerZone.Deletions);
        result.Data.ExpiresAtUtc.Should().Be(Now.UtcDateTime.Add(ArchiveDangerZone.Lifetime));
        _journal.Should().ContainSingle(entry => entry.Action == AuditActions.Archive.DangerZoneEntered);
        events.Verify(e => e.PublishAsync(It.Is<ArchiveDangerZoneEntered>(raised => raised.KurinKey == _kurinKey), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnAdministratorWithNoMemberCard_IsNamedByTheAccount_WhenTheLeadershipIsTold()
    {
        _members.Setup(m => m.FindByAccountAsync(_userKey, It.IsAny<CancellationToken>())).ReturnsAsync((MemberSummary?)null);
        _reauth.Setup(r => r.VerifyAsync(_userKey, "right", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReauthenticationResult(ReauthenticationOutcome.Verified, ReauthenticationMethod.Password, Stamp, "Олена Адмін"));
        var events = new Mock<IDomainEventPublisher>();
        var handler = new EnterArchiveDangerZoneCommandHandler(_unitOfWork.Object, Reach(), Zone(), _reauth.Object, _user.Object, _members.Object, _audit.Object, events.Object);

        await handler.Handle(new EnterArchiveDangerZoneCommand(_kurinKey, "right", null), default);

        events.Verify(e => e.PublishAsync(It.Is<ArchiveDangerZoneEntered>(raised => raised.ActorName == "Олена Адмін"), It.IsAny<CancellationToken>()), Times.Once);
    }

    private CorrectArchivedOfficeCommandHandler CorrectOffice() => new(_unitOfWork.Object, Reach(), _audit.Object);

    [Fact]
    public async Task ACurrentHolding_IsNotCorrectedInTheArchive()
    {
        var office = FormerOffice();
        office.EndDate = null;
        _archive.Setup(a => a.FindOfficeAsync(office.LeadershipHistoryKey, It.IsAny<CancellationToken>())).ReturnsAsync(office);

        var result = await CorrectOffice().Handle(new CorrectArchivedOfficeCommand(_kurinKey, office.LeadershipHistoryKey, LeadershipRole.Pysar, office.StartDate, new DateOnly(2024, 1, 1)), default);

        result.ErrorCode.Should().Be(ArchiveErrors.StillCurrent);
    }

    [Fact]
    public async Task AGurtokLeader_CorrectsTheirGurtok_ButNotTheKurinsProvid()
    {
        Grant(AccessScope.OwnGroups, ResourceAction.Read, ResourceAction.Update);
        var theirs = FormerOffice(groupKey: _ledGroup);
        var kurins = FormerOffice();
        _archive.Setup(a => a.FindOfficeAsync(theirs.LeadershipHistoryKey, It.IsAny<CancellationToken>())).ReturnsAsync(theirs);
        _archive.Setup(a => a.FindOfficeAsync(kurins.LeadershipHistoryKey, It.IsAny<CancellationToken>())).ReturnsAsync(kurins);

        var corrected = await CorrectOffice().Handle(new CorrectArchivedOfficeCommand(_kurinKey, theirs.LeadershipHistoryKey, LeadershipRole.Skarbnyk, new DateOnly(2021, 10, 1), new DateOnly(2023, 8, 31)), default);
        var refused = await CorrectOffice().Handle(new CorrectArchivedOfficeCommand(_kurinKey, kurins.LeadershipHistoryKey, LeadershipRole.Pysar, new DateOnly(2021, 10, 1), new DateOnly(2023, 8, 31)), default);

        corrected.Type.Should().Be(ResultType.Success);
        theirs.Role.Should().Be(LeadershipRole.Skarbnyk);
        theirs.StartDate.Should().Be(new DateOnly(2021, 10, 1));
        var entry = _journal.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditActions.Office.Corrected);
        entry.Changes!.Select(change => change.Field).Should().BeEquivalentTo(["role", "startDate"]);
        refused.ErrorCode.Should().Be(ArchiveErrors.OutOfReach);
    }

    [Fact]
    public async Task ARoleFromAnotherProvid_IsRefused()
    {
        var office = FormerOffice();
        _archive.Setup(a => a.FindOfficeAsync(office.LeadershipHistoryKey, It.IsAny<CancellationToken>())).ReturnsAsync(office);

        var result = await CorrectOffice().Handle(new CorrectArchivedOfficeCommand(_kurinKey, office.LeadershipHistoryKey, LeadershipRole.Hurtkoviy, office.StartDate, office.EndDate!.Value), default);

        result.ErrorCode.Should().Be(ArchiveErrors.InvalidRole);
    }

    private readonly Mock<IDuesDirectory> _dues = new();
    private readonly Mock<IDomainEventPublisher> _events = new();

    private Membership FormerMembership(Guid? memberKey = null) => new()
    {
        MembershipKey = Guid.NewGuid(),
        MemberKey = memberKey ?? Guid.NewGuid(),
        KurinKey = _kurinKey,
        JoinedAtUtc = new DateTime(2019, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        LeftAtUtc = new DateTime(2023, 6, 30, 0, 0, 0, DateTimeKind.Utc)
    };

    private DeleteArchivedMembershipCommandHandler DeleteMembership() => new(_unitOfWork.Object, _members.Object, Gate(), _audit.Object, _dues.Object, _events.Object);
    private CorrectArchivedMembershipCommandHandler CorrectMembership() => new(_unitOfWork.Object, _members.Object, Reach(), _audit.Object, _dues.Object);

    [Fact]
    public async Task AMembershipWithDues_IsNotDeleted()
    {
        // Its charges and payments would stay under a key nobody has: a debt gone, a surplus lost.
        var membership = FormerMembership();
        _archive.Setup(a => a.FindMembershipAsync(membership.MembershipKey, It.IsAny<CancellationToken>())).ReturnsAsync(membership);
        _dues.Setup(d => d.HasLedgerAsync(_kurinKey, It.Is<IReadOnlyCollection<Guid>>(keys => keys.Contains(membership.MembershipKey)), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await DeleteMembership().Handle(new DeleteArchivedMembershipCommand(_kurinKey, membership.MembershipKey, OpenZone()), default);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be(ArchiveErrors.HasDues);
        _archive.Verify(a => a.RemoveMembership(It.IsAny<Membership>()), Times.Never);
    }

    [Fact]
    public async Task AMembershipWhoseOfficeIsStillHeld_IsNotDeleted()
    {
        var membership = FormerMembership();
        _archive.Setup(a => a.FindMembershipAsync(membership.MembershipKey, It.IsAny<CancellationToken>())).ReturnsAsync(membership);
        _archive.Setup(a => a.HoldsOpenOfficeSinceAsync(membership.MemberKey, _kurinKey, new DateOnly(2023, 6, 30), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await DeleteMembership().Handle(new DeleteArchivedMembershipCommand(_kurinKey, membership.MembershipKey, OpenZone()), default);

        result.ErrorCode.Should().Be(ArchiveErrors.OpenOffice);
        _archive.Verify(a => a.RemoveMembership(It.IsAny<Membership>()), Times.Never);
    }

    [Fact]
    public async Task ADeletedMembership_IsAnnounced_SoTheModulesKeyedByItForgetItToo()
    {
        var membership = FormerMembership();
        _archive.Setup(a => a.FindMembershipAsync(membership.MembershipKey, It.IsAny<CancellationToken>())).ReturnsAsync(membership);
        _archive.Setup(a => a.RemoveOfficesHeldAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await DeleteMembership().Handle(new DeleteArchivedMembershipCommand(_kurinKey, membership.MembershipKey, OpenZone()), default);

        result.Type.Should().Be(ResultType.Success);
        _archive.Verify(a => a.RemoveMembership(membership), Times.Once);
        _events.Verify(e => e.PublishAsync(It.Is<MembershipsRemoved>(raised => raised.MembershipKeys.Contains(membership.MembershipKey)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AMembershipWithDues_KeepsItsKindAndDates()
    {
        var membership = FormerMembership();
        _archive.Setup(a => a.FindMembershipAsync(membership.MembershipKey, It.IsAny<CancellationToken>())).ReturnsAsync(membership);
        _archive.Setup(a => a.GetStintGroupKeysAsync(membership.MembershipKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _dues.Setup(d => d.HasLedgerAsync(_kurinKey, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CorrectMembership().Handle(new CorrectArchivedMembershipCommand(_kurinKey, membership.MembershipKey, MembershipKind.Staff, new DateOnly(2019, 9, 1), new DateOnly(2023, 6, 30)), default);

        result.ErrorCode.Should().Be(ArchiveErrors.HasDues);
        membership.Kind.Should().Be(MembershipKind.Youth);
    }

    [Fact]
    public async Task ALeaveDateCorrectedToTheDayTheyJoined_IsNotBeforeTheyJoined()
    {
        var membership = FormerMembership();
        membership.JoinedAtUtc = new DateTime(2019, 9, 1, 14, 0, 0, DateTimeKind.Utc);
        _archive.Setup(a => a.FindMembershipAsync(membership.MembershipKey, It.IsAny<CancellationToken>())).ReturnsAsync(membership);
        _archive.Setup(a => a.GetStintGroupKeysAsync(membership.MembershipKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _archive.Setup(a => a.GetOtherPeriodsAsync(membership.MemberKey, _kurinKey, membership.MembershipKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var leftWas = membership.LeftAtUtc!.Value;

        var result = await CorrectMembership().Handle(new CorrectArchivedMembershipCommand(_kurinKey, membership.MembershipKey, MembershipKind.Youth, new DateOnly(2019, 9, 1), new DateOnly(2019, 9, 1)), default);

        result.Type.Should().Be(ResultType.Success);
        membership.LeftAtUtc.Should().Be(membership.JoinedAtUtc);
        _archive.Verify(a => a.MoveStintEdgesAsync(membership.MembershipKey, membership.JoinedAtUtc, membership.JoinedAtUtc, leftWas, membership.JoinedAtUtc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CorrectedDates_MayNotOverlapAnotherMembershipOfTheirsInTheKurin()
    {
        var membership = FormerMembership();
        _archive.Setup(a => a.FindMembershipAsync(membership.MembershipKey, It.IsAny<CancellationToken>())).ReturnsAsync(membership);
        _archive.Setup(a => a.GetStintGroupKeysAsync(membership.MembershipKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _archive.Setup(a => a.GetOtherPeriodsAsync(membership.MemberKey, _kurinKey, membership.MembershipKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([(new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc), (DateTime?)null)]);

        var overlapping = await CorrectMembership().Handle(new CorrectArchivedMembershipCommand(_kurinKey, membership.MembershipKey, MembershipKind.Youth, new DateOnly(2019, 9, 1), new DateOnly(2024, 3, 1)), default);
        var fine = await CorrectMembership().Handle(new CorrectArchivedMembershipCommand(_kurinKey, membership.MembershipKey, MembershipKind.Youth, new DateOnly(2019, 9, 1), new DateOnly(2023, 12, 31)), default);

        overlapping.ErrorCode.Should().Be(ArchiveErrors.Overlap);
        fine.Type.Should().Be(ResultType.Success);
    }
}
