using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Private;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ScoreModule;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Score;

namespace ProjectK.BusinessLogic.Tests.ScoreModule;

/// <summary>
/// The КВ's book: shared among those who hold <c>KurinScorePrivate</c>, closed to everyone else, and
/// never a part of what the table or the youth sees.
/// </summary>
public class PrivateScoreTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Sokoly = Guid.NewGuid();

    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero));
    private readonly List<PrivateScoreCriterion> _criteria = [];
    private readonly List<PrivateScoreEntry> _entries = [];
    private readonly List<ScoreEntry> _publicEntries = [];
    private readonly List<KurinMembershipRecord> _memberships = [];
    private readonly List<MemberSummary> _people = [];
    private readonly Mock<IScoreUnitOfWork> _score = new();
    private readonly Mock<IResourceAccessService> _resourceAccess = new();
    private readonly Guid _oksana;
    private readonly PrivateScoreCriterion _leadership = new() { KurinKey = Kurin, Name = "Лідерство" };

    public PrivateScoreTests()
    {
        _oksana = Youth("Оксана");
        _criteria.Add(_leadership);

        var criteria = new Mock<IPrivateScoreCriterionRepository>();
        criteria.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _criteria);
        var entries = new Mock<IPrivateScoreEntryRepository>();
        entries.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _entries.Where(e => !e.IsDeleted).ToList());
        entries.Setup(r => r.Create(It.IsAny<PrivateScoreEntry>(), It.IsAny<CancellationToken>())).Callback<PrivateScoreEntry, CancellationToken>((e, _) => _entries.Add(e));
        var trail = new Mock<IScoreTrailEventRepository>();
        _score.SetupGet(s => s.PrivateScoreCriteria).Returns(criteria.Object);
        _score.SetupGet(s => s.PrivateScoreEntries).Returns(entries.Object);
        _score.SetupGet(s => s.ScoreTrailEvents).Returns(trail.Object);

        IReadOnlyList<ScoreEntry> publicEntries = _publicEntries;
        _score.SetupGet(s => s.ScoreEntries).Returns(Mock.Of<IScoreEntryRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(publicEntries)));
        _score.SetupGet(s => s.KurinScoreSettings).Returns(Mock.Of<IKurinScoreSettingsRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult<KurinScoreSettings?>(null)));
        var rules = None<ScoreRule>();
        var rates = None<ScoreAttendanceRate>();
        var items = None<ScoreItem>();
        var stages = None<ScoreStage>();
        var moves = None<ScoreGroupMove>();
        var marks = None<ScoreAttendance>();
        _score.SetupGet(s => s.ScoreRules).Returns(Mock.Of<IScoreRuleRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(rules)));
        _score.SetupGet(s => s.ScoreAttendanceRates).Returns(Mock.Of<IScoreAttendanceRateRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(rates)));
        _score.SetupGet(s => s.ScoreItems).Returns(Mock.Of<IScoreItemRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(items)));
        _score.SetupGet(s => s.ScoreStages).Returns(Mock.Of<IScoreStageRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(stages)));
        _score.SetupGet(s => s.ScoreGroupMoves).Returns(Mock.Of<IScoreGroupMoveRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(moves)));
        _score.SetupGet(s => s.ScoreAttendances).Returns(Mock.Of<IScoreAttendanceRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(marks)));

        // The Звʼязковий: everything.
        _resourceAccess.Setup(r => r.CheckAccessAsync(It.IsAny<ResourceType>(), It.IsAny<ResourceAction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());
    }

    private static IReadOnlyList<T> None<T>() => [];

    private Guid Youth(string name)
    {
        var membership = Guid.NewGuid();
        var member = Guid.NewGuid();
        _memberships.Add(new KurinMembershipRecord(membership, member, Sokoly, MembershipKind.Youth, new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc), null));
        _people.Add(new MemberSummary(member, Guid.NewGuid(), Kurin, Sokoly, name, "Пластун", $"{name}@x", null));
        return membership;
    }

    private PrivateScoreAccess Access()
    {
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(u => u.UserId).Returns(Actor);
        user.SetupGet(u => u.KurinKey).Returns(Kurin);
        return new PrivateScoreAccess(new ScoreAccess(user.Object, _resourceAccess.Object), _resourceAccess.Object);
    }

    private ICurrentUserContext User()
    {
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(u => u.UserId).Returns(Actor);
        user.SetupGet(u => u.KurinKey).Returns(Kurin);
        return user.Object;
    }

    private ScoreBookReader Books()
    {
        var memberships = new Mock<IMembershipDirectory>();
        memberships.Setup(d => d.GetInKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _memberships);
        var members = new Mock<IMemberDirectory>();
        members.Setup(m => m.GetByKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _people);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Groups).Returns(Mock.Of<IGroupRepository>(g =>
            g.GetAllAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult<IEnumerable<Group>>(new[] { new Group("Соколи", Kurin, null) { GroupKey = Sokoly } })));
        unitOfWork.SetupGet(u => u.AgendaItems).Returns(new Mock<IAgendaItemRepository>().Object);
        IReadOnlyList<ScoreFact> none = [];
        var facts = Mock.Of<IScoreFactSource>(f => f.ForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(none));
        return new ScoreBookReader(_score.Object, unitOfWork.Object, memberships.Object, members.Object, facts, _time);
    }

    private Task<ServiceResult<Guid>> Write(int points, Guid? criterion, string? note = null) =>
        new CreatePrivateScoreEntryCommandHandler(Access(), Books(), _score.Object, User(), _time).Handle(
            new CreatePrivateScoreEntryCommand(Kurin, new UpsertPrivateScoreEntryRequest
            {
                MembershipKey = _oksana,
                PrivateScoreCriterionKey = criterion,
                Points = points,
                Note = note,
                OccurredOn = new DateOnly(2026, 10, 6)
            }),
            CancellationToken.None);

    private Task<ServiceResult<PrivateScoreDto>> Read() =>
        new GetPrivateScoreQueryHandler(Access(), Books(), _score.Object).Handle(new GetPrivateScoreQuery(Kurin, new ScorePeriodQuery()), CancellationToken.None);

    [Fact]
    public async Task TheBook_TotalsByCriterion_WithThePublicPointsAsTheGround()
    {
        _publicEntries.Add(new ScoreEntry { KurinKey = Kurin, MembershipKey = _oksana, Points = 4, Reason = "x", OccurredOn = new DateOnly(2026, 10, 5) });
        (await Write(3, _leadership.PrivateScoreCriterionKey)).Type.Should().Be(ResultType.Created);
        (await Write(-1, null, "запізнилась на раду")).Type.Should().Be(ResultType.Created);

        var book = (await Read()).Data!;

        var oksana = book.People.Single();
        oksana.Should().BeEquivalentTo(new { PublicTotal = 4, PrivateTotal = 2, Uncategorised = -1 });
        oksana.ByCriterion.Should().Equal(new Dictionary<Guid, int> { [_leadership.PrivateScoreCriterionKey] = 3 });
        book.Entries.Should().HaveCount(2).And.Contain(e => e.CriterionName == "Лідерство");
    }

    [Fact]
    public async Task NothingWithoutAWord_AndNoCriterionOffTheList()
    {
        new UpsertPrivateScoreEntryRequestValidator(_time)
            .Validate(new UpsertPrivateScoreEntryRequest { MembershipKey = _oksana, Points = 2, OccurredOn = new DateOnly(2026, 10, 6) })
            .IsValid.Should().BeFalse("neither a criterion nor a note");

        (await Write(2, Guid.NewGuid())).Type.Should().Be(ResultType.NotFound);
    }

    [Fact]
    public async Task WhoeverLacksTheGrant_SeesNothing_AndWritesNothing()
    {
        _resourceAccess.Setup(r => r.CheckAccessAsync(ResourceType.KurinScorePrivate, It.IsAny<ResourceAction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Deny("not КВ"));

        (await Read()).Type.Should().Be(ResultType.Forbidden);
        (await Write(3, _leadership.PrivateScoreCriterionKey)).Type.Should().Be(ResultType.Forbidden);
        _entries.Should().BeEmpty();
    }

    // The private book never leaks into the public ledger: the ScoreBook reads only ScoreEntries.
    [Fact]
    public async Task PrivatePoints_NeverReachThePublicLedger()
    {
        await Write(50, _leadership.PrivateScoreCriterionKey);

        var book = await Books().OpenAsync(Kurin, CancellationToken.None);

        book.Ledger.People(new ScorePeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31))).Should().BeEmpty();
    }
}
