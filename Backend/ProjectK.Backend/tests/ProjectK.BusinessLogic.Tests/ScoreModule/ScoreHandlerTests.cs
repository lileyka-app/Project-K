using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Entry;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Sheet.Get;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Sheet.Mark;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Sheet.NotHeld;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Exceptions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Score;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.ScoreModule;

/// <summary>
/// The sheet and the entries as a гуртковий суддя of «Соколи» sees them: his own гурток is his to
/// mark and score, «Леви» is not, and what someone else already put down stays theirs.
/// </summary>
public class ScoreHandlerTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Sokoly = Guid.NewGuid();
    private static readonly Guid Levy = Guid.NewGuid();
    private static readonly Guid Skhodyny = Guid.NewGuid();
    private static readonly DateTime Start = new(2026, 10, 6, 16, 0, 0, DateTimeKind.Utc);

    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 6, 18, 0, 0, TimeSpan.Zero));
    private readonly List<ScoreAttendance> _marks = [];
    private readonly List<ScoreEntry> _entries = [];
    private readonly List<ScoreItem> _items = [];
    private readonly List<ScoreTrailEvent> _trail = [];
    private readonly List<ScoreNotHeldOccurrence> _notHeld = [];
    private readonly List<AuditRecord> _journal = [];
    private readonly Mock<IKurinAudit> _audit = new();
    private readonly List<KurinMembershipRecord> _memberships = [];
    private readonly List<MemberSummary> _people = [];
    // People who stand in no kurin any more: the directory reads them only by key.
    private readonly List<MemberSummary> _gone = [];
    private readonly List<AgendaResponse> _responses = [];
    private readonly AgendaItem _event;
    private readonly Mock<IScoreUnitOfWork> _score = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IResourceAccessService> _resourceAccess = new();

    private readonly Guid _oksana;
    private readonly Guid _taras;

    public ScoreHandlerTests()
    {
        _event = new AgendaItem { AgendaItemKey = Skhodyny, KurinKey = Kurin, Kind = AgendaItemKind.Event, Title = "Сходини", StartUtc = Start, IsAllDay = false };
        _event.Assignments.Add(new AgendaAssignment { AgendaItemKey = Skhodyny, TargetType = AgendaTargetType.Group, TargetKey = Sokoly });

        _oksana = Youth("Оксана", Sokoly);
        _taras = Youth("Тарас", Levy);

        var marks = new Mock<IScoreAttendanceRepository>();
        marks.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _marks.Where(m => !m.IsRemoved).ToList());
        marks.Setup(r => r.GetStandingAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid m, Guid e, DateTime o, CancellationToken _) =>
                _marks.FirstOrDefault(x => x.MembershipKey == m && x.AgendaItemKey == e && x.OccurrenceStartUtc == o && !x.IsRemoved));
        marks.Setup(r => r.Create(It.IsAny<ScoreAttendance>(), It.IsAny<CancellationToken>())).Callback<ScoreAttendance, CancellationToken>((m, _) => _marks.Add(m));

        var entries = new Mock<IScoreEntryRepository>();
        entries.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _entries.Where(e => !e.IsDeleted).ToList());
        entries.Setup(r => r.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid k, CancellationToken _) => _entries.FirstOrDefault(e => e.ScoreEntryKey == k));
        entries.Setup(r => r.Create(It.IsAny<ScoreEntry>(), It.IsAny<CancellationToken>())).Callback<ScoreEntry, CancellationToken>((e, _) => _entries.Add(e));

        var items = new Mock<IScoreItemRepository>();
        items.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _items);
        var trail = new Mock<IScoreTrailEventRepository>();
        trail.Setup(r => r.Create(It.IsAny<ScoreTrailEvent>(), It.IsAny<CancellationToken>())).Callback<ScoreTrailEvent, CancellationToken>((e, _) => _trail.Add(e));

        _score.SetupGet(s => s.ScoreAttendances).Returns(marks.Object);
        _score.SetupGet(s => s.ScoreEntries).Returns(entries.Object);
        _score.SetupGet(s => s.ScoreItems).Returns(items.Object);
        _score.SetupGet(s => s.ScoreTrailEvents).Returns(trail.Object);
        var notHeld = new Mock<IScoreNotHeldOccurrenceRepository>();
        notHeld.Setup(r => r.FindAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid e, DateTime o, CancellationToken _) => _notHeld.FirstOrDefault(n => n.AgendaItemKey == e && n.OccurrenceStartUtc == o));
        notHeld.Setup(r => r.Create(It.IsAny<ScoreNotHeldOccurrence>(), It.IsAny<CancellationToken>())).Callback<ScoreNotHeldOccurrence, CancellationToken>((n, _) => _notHeld.Add(n));
        notHeld.Setup(r => r.Delete(It.IsAny<ScoreNotHeldOccurrence>(), It.IsAny<CancellationToken>())).Callback<ScoreNotHeldOccurrence, CancellationToken>((n, _) => _notHeld.Remove(n));
        _score.SetupGet(s => s.ScoreNotHeldOccurrences).Returns(notHeld.Object);
        _audit.Setup(a => a.RecordAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>())).Callback<AuditRecord, CancellationToken>((r, _) => _journal.Add(r)).Returns(Task.CompletedTask);
        _score.SetupGet(s => s.KurinScoreSettings).Returns(Empty<IKurinScoreSettingsRepository, KurinScoreSettings>(r => r.Setup(x => x.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync((KurinScoreSettings?)null)));
        _score.SetupGet(s => s.ScoreRules).Returns(Empty<IScoreRuleRepository, ScoreRule>(r => r.Setup(x => x.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync([])));
        _score.SetupGet(s => s.ScoreAttendanceRates).Returns(Empty<IScoreAttendanceRateRepository, ScoreAttendanceRate>(r => r.Setup(x => x.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync([])));
        _score.SetupGet(s => s.ScoreStages).Returns(Empty<IScoreStageRepository, ScoreStage>(r => r.Setup(x => x.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync([])));
        _score.SetupGet(s => s.ScoreGroupMoves).Returns(Empty<IScoreGroupMoveRepository, ScoreGroupMove>(r => r.Setup(x => x.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync([])));

        var groups = new Mock<IGroupRepository>();
        groups.Setup(g => g.GetAllAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new Group("Соколи", Kurin, null) { GroupKey = Sokoly },
            new Group("Леви", Kurin, null) { GroupKey = Levy }
        ]);
        var agenda = new Mock<IAgendaItemRepository>();
        agenda.Setup(a => a.GetByKeyWithAssignmentsAsync(Skhodyny, It.IsAny<CancellationToken>())).ReturnsAsync(_event);
        agenda.Setup(a => a.GetByKeyAsync(Skhodyny, It.IsAny<CancellationToken>())).ReturnsAsync(_event);
        agenda.Setup(a => a.GetCategoryKeysAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> keys, CancellationToken _) =>
                keys.Where(k => k == Skhodyny).ToDictionary(k => k, _ => _event.AgendaCategoryKey));
        var responses = new Mock<IAgendaResponseRepository>();
        responses.Setup(r => r.GetForItemAsync(Skhodyny, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _responses);
        _unitOfWork.SetupGet(u => u.Groups).Returns(groups.Object);
        _unitOfWork.SetupGet(u => u.AgendaItems).Returns(agenda.Object);
        _unitOfWork.SetupGet(u => u.AgendaResponses).Returns(responses.Object);
        _unitOfWork.SetupGet(u => u.AgendaCategories).Returns(new Mock<IAgendaCategoryRepository>().Object);

        // A гуртковий суддя of Соколи: Соколи yes, Леви no, the whole kurin no.
        _resourceAccess.Setup(r => r.CheckAccessAsync(ResourceType.GroupScore, It.IsAny<ResourceAction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceType _, ResourceAction _, Guid key, CancellationToken _) => key == Sokoly ? ResourceAccessDecision.Allow() : ResourceAccessDecision.Deny("not yours"));
        _resourceAccess.Setup(r => r.CheckAccessAsync(It.IsAny<ResourceType>(), It.IsAny<ResourceAction>(), It.IsAny<ResourceType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Deny("not yours"));
        _resourceAccess.Setup(r => r.CheckAccessAsync(ResourceType.KurinScore, ResourceAction.Manage, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Deny("not yours"));
    }

    private static TRepo Empty<TRepo, TEntity>(Action<Mock<TRepo>> setup) where TRepo : class
    {
        var mock = new Mock<TRepo>();
        setup(mock);
        return mock.Object;
    }

    private Guid Youth(string name, Guid? group)
    {
        var membership = Guid.NewGuid();
        var member = Guid.NewGuid();
        _memberships.Add(new KurinMembershipRecord(membership, member, group, MembershipKind.Youth, new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc), null));
        _people.Add(new MemberSummary(member, Guid.NewGuid(), Kurin, group, name, "Пластун", $"{name}@x", null));
        return membership;
    }

    private ScoreAccess Access()
    {
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(u => u.UserId).Returns(Actor);
        user.SetupGet(u => u.KurinKey).Returns(Kurin);
        return new ScoreAccess(user.Object, _resourceAccess.Object);
    }

    private ICurrentUserContext User()
    {
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(u => u.UserId).Returns(Actor);
        user.SetupGet(u => u.KurinKey).Returns(Kurin);
        return user.Object;
    }

    private IMembershipDirectory Memberships()
    {
        var directory = new Mock<IMembershipDirectory>();
        directory.Setup(d => d.GetInKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _memberships);
        return directory.Object;
    }

    private static IScoreFactSource NoFacts()
    {
        IReadOnlyList<ScoreFact> none = [];
        return Mock.Of<IScoreFactSource>(f => f.ForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(none));
    }

    private ScoreBookReader Books()
    {
        var members = new Mock<IMemberDirectory>();
        members.Setup(m => m.GetByKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _people);
        members.Setup(m => m.GetByKeysAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> keys, CancellationToken _) => _gone.Where(p => keys.Contains(p.MemberKey)).ToList());
        return new ScoreBookReader(_score.Object, _unitOfWork.Object, Memberships(), members.Object, NoFacts(), _time);
    }

    private MarkAttendanceCommandHandler Marker() => new(Access(), _score.Object, _unitOfWork.Object, Memberships(), Books(), User(), _time, Mock.Of<IKraiProfileProvider>(), _audit.Object);

    private CreateScoreEntryCommandHandler Giver() => new(Access(), Books(), _score.Object, _unitOfWork.Object, User(), _time, Mock.Of<IKraiProfileProvider>());

    private static MarkAttendanceCommand MarkCommand(params Guid[] keys) =>
        new(Kurin, Skhodyny, Start, new MarkAttendanceRequest { MembershipKeys = keys });

    [Fact]
    public async Task TheSheet_SaysWhoAnswered_WhoWasAimedAt_AndWhoIsMine()
    {
        var oksanaUser = _people.Single(p => p.FirstName == "Оксана").UserKey!.Value;
        _responses.Add(new AgendaResponse { AgendaItemKey = Skhodyny, UserKey = oksanaUser, Status = AgendaRsvpStatus.Going });

        var result = await new GetAttendanceSheetQueryHandler(Access(), Books(), _unitOfWork.Object, Mock.Of<IKraiProfileProvider>())
            .Handle(new GetAttendanceSheetQuery(Kurin, Skhodyny, Start), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        var oksana = result.Data!.People.Single(p => p.MembershipKey == _oksana);
        var taras = result.Data.People.Single(p => p.MembershipKey == _taras);
        oksana.Should().BeEquivalentTo(new { Rsvp = AgendaRsvpStatus.Going, IsAssigned = true, CanScore = true });
        taras.Should().BeEquivalentTo(new { Rsvp = (AgendaRsvpStatus?)null, IsAssigned = false, CanScore = false });
    }

    [Fact]
    public async Task TheSheet_IsNotForSomeoneWhoScoresNowhere()
    {
        _resourceAccess.Setup(r => r.CheckAccessAsync(ResourceType.GroupScore, It.IsAny<ResourceAction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Deny("youth"));

        var result = await new GetAttendanceSheetQueryHandler(Access(), Books(), _unitOfWork.Object, Mock.Of<IKraiProfileProvider>())
            .Handle(new GetAttendanceSheetQuery(Kurin, Skhodyny, Start), CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
    }

    [Fact]
    public async Task ADayTheSeriesDoesNotMeet_IsNothing()
    {
        var result = await Marker().Handle(
            new MarkAttendanceCommand(Kurin, Skhodyny, Start.AddDays(1), new MarkAttendanceRequest { MembershipKeys = [_oksana] }),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
        _marks.Should().BeEmpty();
    }

    [Fact]
    public async Task Marking_WritesTheMark_AndItsTrail()
    {
        var result = await Marker().Handle(MarkCommand(_oksana), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.Single().Outcome.Should().Be("Marked");
        _marks.Should().ContainSingle().Which.Should().BeEquivalentTo(new { MembershipKey = _oksana, AgendaItemKey = Skhodyny, OccurrenceStartUtc = Start, MarkedByUserKey = (Guid?)Actor });
        _trail.Should().ContainSingle(t => t.Subject == ScoreTrail.Attendance && t.Action == ScoreTrail.Created);
        _score.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    // The second суддя does not overwrite the first: the mark is one, and the result says whose.
    [Fact]
    public async Task SomeoneAlreadyMarked_StaysMarkedByWhoeverDidIt()
    {
        _marks.Add(new ScoreAttendance { KurinKey = Kurin, MembershipKey = _oksana, AgendaItemKey = Skhodyny, OccurrenceStartUtc = Start, MarkedByUserKey = Other, MarkedAtUtc = Start });

        var result = await Marker().Handle(MarkCommand(_oksana), CancellationToken.None);

        result.Data!.Single().Outcome.Should().Be("AlreadyMarked");
        _marks.Should().ContainSingle().Which.MarkedByUserKey.Should().Be(Other);
    }

    [Fact]
    public async Task AnotherGurtoksYouth_IsNotMineToMark()
    {
        var result = await Marker().Handle(MarkCommand(_oksana, _taras), CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
        _score.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    private SetOccurrenceNotHeldCommandHandler NotHeldSetter() =>
        new(Access(), _score.Object, _unitOfWork.Object, User(), _time, Mock.Of<IKraiProfileProvider>(), _audit.Object);

    [Fact]
    public async Task NotHeld_IsSaid_ByWhoeverMarksSomewhere_AndJournalled()
    {
        var result = await NotHeldSetter().Handle(new SetOccurrenceNotHeldCommand(Kurin, Skhodyny, Start, true), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _notHeld.Should().ContainSingle().Which.Should().BeEquivalentTo(new { KurinKey = Kurin, AgendaItemKey = Skhodyny, OccurrenceStartUtc = Start, MarkedByUserKey = (Guid?)Actor });
        _journal.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Area = AuditArea.Agenda,
            Action = AuditActions.Agenda.OccurrenceNotHeld,
            SubjectKey = Skhodyny,
            SubjectLabel = "Сходини · 06.10.2026"
        });
        _score.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task NotHeld_IsNotForSomeoneWhoMarksNowhere()
    {
        _resourceAccess.Setup(r => r.CheckAccessAsync(ResourceType.GroupScore, It.IsAny<ResourceAction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Deny("youth"));

        var result = await NotHeldSetter().Handle(new SetOccurrenceNotHeldCommand(Kurin, Skhodyny, Start, true), CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
        _notHeld.Should().BeEmpty();
    }

    [Fact]
    public async Task NotHeld_OnADayTheSeriesDoesNotMeet_IsNothing()
    {
        var result = await NotHeldSetter().Handle(new SetOccurrenceNotHeldCommand(Kurin, Skhodyny, Start.AddDays(1), true), CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
    }

    [Fact]
    public async Task NotHeld_IsRefused_WhileSomeoneIsMarkedPresent()
    {
        _marks.Add(new ScoreAttendance { KurinKey = Kurin, MembershipKey = _oksana, AgendaItemKey = Skhodyny, OccurrenceStartUtc = Start, MarkedByUserKey = Other, MarkedAtUtc = Start });

        var result = await NotHeldSetter().Handle(new SetOccurrenceNotHeldCommand(Kurin, Skhodyny, Start, true), CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("OccurrenceHasAttendance");
        _notHeld.Should().BeEmpty();
    }

    [Fact]
    public async Task NotHeld_TakenBack_RemovesIt_AndSaysSo()
    {
        _notHeld.Add(new ScoreNotHeldOccurrence { KurinKey = Kurin, AgendaItemKey = Skhodyny, OccurrenceStartUtc = Start, MarkedByUserKey = Other });

        var result = await NotHeldSetter().Handle(new SetOccurrenceNotHeldCommand(Kurin, Skhodyny, Start, false), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _notHeld.Should().BeEmpty();
        _journal.Should().ContainSingle().Which.Action.Should().Be(AuditActions.Agenda.OccurrenceHeld);
    }

    [Fact]
    public async Task NotHeld_AskedAgain_ChangesNothing()
    {
        await NotHeldSetter().Handle(new SetOccurrenceNotHeldCommand(Kurin, Skhodyny, Start, false), CancellationToken.None);
        await NotHeldSetter().Handle(new SetOccurrenceNotHeldCommand(Kurin, Skhodyny, Start, true), CancellationToken.None);
        await NotHeldSetter().Handle(new SetOccurrenceNotHeldCommand(Kurin, Skhodyny, Start, true), CancellationToken.None);

        _notHeld.Should().ContainSingle();
        _journal.Should().ContainSingle();
    }

    // A meeting someone was at took place: the first mark takes «did not take place» back.
    [Fact]
    public async Task Marking_ANotHeldOccurrence_TakesNotHeldBack()
    {
        _notHeld.Add(new ScoreNotHeldOccurrence { KurinKey = Kurin, AgendaItemKey = Skhodyny, OccurrenceStartUtc = Start, MarkedByUserKey = Other });

        var result = await Marker().Handle(MarkCommand(_oksana), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _marks.Should().ContainSingle();
        _notHeld.Should().BeEmpty();
        _journal.Should().ContainSingle().Which.Action.Should().Be(AuditActions.Agenda.OccurrenceHeld);
    }

    [Fact]
    public async Task TheSheet_SaysWhoSaidItDidNotTakePlace()
    {
        _notHeld.Add(new ScoreNotHeldOccurrence { KurinKey = Kurin, AgendaItemKey = Skhodyny, OccurrenceStartUtc = Start, MarkedByUserKey = Actor, MarkedAtUtc = Start });

        var result = await new GetAttendanceSheetQueryHandler(Access(), Books(), _unitOfWork.Object, Mock.Of<IKraiProfileProvider>())
            .Handle(new GetAttendanceSheetQuery(Kurin, Skhodyny, Start), CancellationToken.None);

        result.Data!.NotHeld.Should().NotBeNull();
        result.Data.NotHeld!.MarkedAtUtc.Should().Be(Start);
    }

    [Fact]
    public async Task APosition_CopiesItsPoints_AndIsGivenOnceAtAnEvent()
    {
        var odnostriy = new ScoreItem { KurinKey = Kurin, Name = "Однострій", Points = 1 };
        _items.Add(odnostriy);
        var request = new UpsertScoreEntryRequest { MembershipKey = _oksana, ScoreItemKey = odnostriy.ScoreItemKey, AgendaItemKey = Skhodyny, OccurrenceStartUtc = Start, OccurredOn = new DateOnly(2026, 10, 6) };

        var first = await Giver().Handle(new CreateScoreEntryCommand(Kurin, request), CancellationToken.None);
        var second = await Giver().Handle(new CreateScoreEntryCommand(Kurin, request), CancellationToken.None);

        first.Type.Should().Be(ResultType.Created);
        _entries.Should().ContainSingle().Which.Points.Should().Be(1);
        second.Type.Should().Be(ResultType.Conflict);
    }

    // Two judges give the same position at once: the unique index catches the slower one, which
    // must hear the same 409 the preflight check gives, not a 500.
    [Fact]
    public async Task APositionGivenMeanwhile_IsAConflict_NotAnError()
    {
        var odnostriy = new ScoreItem { KurinKey = Kurin, Name = "Однострій", Points = 1 };
        _items.Add(odnostriy);
        _score.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DuplicateRowException(new InvalidOperationException()));
        var request = new UpsertScoreEntryRequest { MembershipKey = _oksana, ScoreItemKey = odnostriy.ScoreItemKey, AgendaItemKey = Skhodyny, OccurrenceStartUtc = Start, OccurredOn = new DateOnly(2026, 10, 6) };

        var result = await Giver().Handle(new CreateScoreEntryCommand(Kurin, request), CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("ItemAlreadyGiven");
    }

    // Two judges mark the same youth at once. The slower save fails on the unique index; its second
    // pass sees the other judge's mark and says so instead of failing the whole sheet.
    [Fact]
    public async Task AMarkMadeMeanwhile_IsAlreadyMarked_NotAnError()
    {
        var saves = 0;
        _score.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(() =>
        {
            if (++saves > 1)
            {
                return Task.FromResult(1);
            }

            _marks.Clear();
            _marks.Add(new ScoreAttendance { KurinKey = Kurin, MembershipKey = _oksana, AgendaItemKey = Skhodyny, OccurrenceStartUtc = Start, MarkedByUserKey = Guid.NewGuid() });
            throw new DuplicateRowException(new InvalidOperationException());
        });

        var result = await Marker().Handle(MarkCommand(_oksana), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().ContainSingle().Which.Outcome.Should().Be("AlreadyMarked");
    }

    // A youth who left keeps the гурток they left from: what they earned before leaving still
    // counts for it, and so do the days they were there.
    [Fact]
    public async Task AYouthWhoLeft_KeepsWhatTheyEarned_ForTheirGurtok()
    {
        var left = Guid.NewGuid();
        var member = Guid.NewGuid();
        _memberships.Add(new KurinMembershipRecord(left, member, Sokoly, MembershipKind.Youth, new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc)));
        _people.Add(new MemberSummary(member, Guid.NewGuid(), Kurin, Sokoly, "Ярема", "Пластун", "yarema@x", null));
        _entries.Add(new ScoreEntry { KurinKey = Kurin, MembershipKey = left, Points = 4, Reason = "ватра", OccurredOn = new DateOnly(2026, 10, 1) });

        var book = await Books().OpenAsync(Kurin, CancellationToken.None);
        var sokoly = book.Ledger.Groups(new ScorePeriod(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 6)), [Sokoly, Levy])
            .Single(g => g.GroupKey == Sokoly);

        sokoly.YouthPoints.Should().Be(4);
        sokoly.YouthCount.Should().BeGreaterThan(1, "the youth who left counts for the days before leaving");
    }

    // The kurin's people are only those who stand in it now; someone who left is read by key so
    // their row in the гурток's table keeps a name.
    [Fact]
    public async Task AYouthWhoLeft_IsStillNamed()
    {
        var left = Guid.NewGuid();
        var member = Guid.NewGuid();
        _memberships.Add(new KurinMembershipRecord(left, member, Sokoly, MembershipKind.Youth, new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc)));
        _gone.Add(new MemberSummary(member, null, Guid.Empty, null, "Ярема", "Пластун", "yarema@x", null));

        var book = await Books().OpenAsync(Kurin, CancellationToken.None);

        book.NameOfMembership(left).Should().Be("Ярема Пластун");
        book.CurrentYouths().Should().NotContain(y => y.Membership.MembershipKey == left);
    }

    [Fact]
    public async Task AFreeEntry_MayBeGivenAgain()
    {
        var request = new UpsertScoreEntryRequest { MembershipKey = _oksana, Points = 2, Reason = "ватра", AgendaItemKey = Skhodyny, OccurrenceStartUtc = Start, OccurredOn = new DateOnly(2026, 10, 6) };

        await Giver().Handle(new CreateScoreEntryCommand(Kurin, request), CancellationToken.None);
        var second = await Giver().Handle(new CreateScoreEntryCommand(Kurin, request), CancellationToken.None);

        second.Type.Should().Be(ResultType.Created);
        _entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task PointsToAGurtok_NeedTheRightToScoreIt()
    {
        var mine = await Giver().Handle(new CreateScoreEntryCommand(Kurin, new UpsertScoreEntryRequest { GroupKey = Sokoly, Points = 5, Reason = "виступ", OccurredOn = new DateOnly(2026, 10, 6) }), CancellationToken.None);
        var theirs = await Giver().Handle(new CreateScoreEntryCommand(Kurin, new UpsertScoreEntryRequest { GroupKey = Levy, Points = 5, Reason = "виступ", OccurredOn = new DateOnly(2026, 10, 6) }), CancellationToken.None);

        mine.Type.Should().Be(ResultType.Created);
        theirs.Type.Should().Be(ResultType.Forbidden);
    }

    [Theory]
    [InlineData(true, false, false, "both a person and a гурток")]
    [InlineData(false, true, false, "nobody")]
    [InlineData(true, true, true, "a position and an amount both missing")]
    public void TheShape_IsChecked(bool person, bool noGroup, bool noPoints, string because)
    {
        var request = new UpsertScoreEntryRequest
        {
            MembershipKey = person ? _oksana : null,
            GroupKey = noGroup ? null : Sokoly,
            Points = noPoints ? null : 3,
            Reason = noPoints ? null : "x",
            OccurredOn = new DateOnly(2026, 10, 6)
        };

        new UpsertScoreEntryRequestValidator(_time).Validate(request).IsValid.Should().BeFalse(because);
    }
}
