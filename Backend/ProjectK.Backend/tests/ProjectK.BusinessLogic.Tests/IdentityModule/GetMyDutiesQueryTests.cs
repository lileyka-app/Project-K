using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Modules.IdentityModule.Features.Duties;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.DuesModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Records.AgendaModule;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Records.MemberModule;

namespace ProjectK.BusinessLogic.Tests.IdentityModule;

/// <summary>
/// «Справи» of a Звʼязковий who scores the whole kurin: a past сходини worth points that nobody
/// marked waits on him, until someone marks it or says it did not take place.
/// </summary>
public class GetMyDutiesQueryTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Person = Guid.NewGuid();
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Skhodyny = Guid.NewGuid();
    private static readonly DateTime Start = new(2026, 10, 6, 16, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Bobry = Guid.NewGuid();
    private static readonly MembershipRecord Standing = new(Guid.NewGuid(), Kurin, 1, KurinBranch.UPYu, null, null, null, MembershipKind.Staff, Start.AddYears(-1), null);

    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
    private readonly List<Permission> _permissions = [new(ResourceType.GroupScore, ResourceAction.Create, AccessScope.KurinWide)];
    private readonly List<ScoreAttendance> _marks = [];
    private readonly List<ScoreNotHeldOccurrence> _notHeld = [];
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IScoreUnitOfWork> _score = new();
    private readonly Mock<IDuesUnitOfWork> _dues = new();
    private readonly Mock<ICurrentUserContext> _user = new();
    private readonly List<DismissedDuty> _dismissed = [];
    private readonly Mock<IMemberProgressDirectory> _progress = new();
    private readonly List<DuesEntry> _duesEntries = [];
    private int _submittedBadges;

    public GetMyDutiesQueryTests()
    {
        _user.SetupGet(u => u.UserId).Returns(User);
        _user.SetupGet(u => u.KurinKey).Returns(Kurin);

        var item = new AgendaItem { AgendaItemKey = Skhodyny, KurinKey = Kurin, Kind = AgendaItemKind.Event, Title = "Сходини", StartUtc = Start };
        var agenda = new Mock<IAgendaItemRepository>();
        agenda.Setup(a => a.GetForViewerAsync(It.IsAny<AgendaViewerScope>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), true, AgendaItemKind.Event,
                It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync([item]);
        var leaderships = new Mock<ILeadershipRepository>();
        leaderships.Setup(l => l.GetActiveLeadershipKeysForMemberAsync(Person, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _unitOfWork.SetupGet(u => u.AgendaItems).Returns(agenda.Object);
        _unitOfWork.SetupGet(u => u.Leaderships).Returns(leaderships.Object);
        _unitOfWork.SetupGet(u => u.KurinTags).Returns(TestHelpers.NoKurinTags.Repository());
        var dismissed = new Mock<IDismissedDutyRepository>();
        dismissed.Setup(d => d.GetForUserAsync(User, It.IsAny<CancellationToken>())).ReturnsAsync(() => _dismissed);
        _unitOfWork.SetupGet(u => u.DismissedDuties).Returns(dismissed.Object);
        // A kurin the repository does not know keeps точкування, as every kurin did before the switch.
        _unitOfWork.SetupGet(u => u.Kurins).Returns(Mock.Of<IKurinRepository>());
        var groups = new Mock<IGroupRepository>();
        groups.Setup(g => g.GetAllAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync([new Group("Бобри", Kurin) { GroupKey = Bobry }]);
        _unitOfWork.SetupGet(u => u.Groups).Returns(groups.Object);
        var entries = new Mock<IDuesEntryRepository>();
        entries.Setup(e => e.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _duesEntries);
        _dues.SetupGet(d => d.DuesEntries).Returns(entries.Object);
        _progress.Setup(p => p.CountSubmittedBadgesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _submittedBadges);

        var rates = new Mock<IScoreAttendanceRateRepository>();
        rates.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ScoreAttendanceRate { KurinKey = Kurin, AgendaItemKey = Skhodyny, Points = 2 }]);
        var marks = new Mock<IScoreAttendanceRepository>();
        marks.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _marks);
        var notHeld = new Mock<IScoreNotHeldOccurrenceRepository>();
        notHeld.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _notHeld);
        _score.SetupGet(s => s.ScoreAttendanceRates).Returns(rates.Object);
        _score.SetupGet(s => s.ScoreAttendances).Returns(marks.Object);
        _score.SetupGet(s => s.ScoreNotHeldOccurrences).Returns(notHeld.Object);
    }

    private GetMyDutiesQueryHandler Handler()
    {
        var memberships = new Mock<IMembershipDirectory>();
        memberships.Setup(m => m.GetCurrentForAccountAsync(User, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Standing]);
        var members = new Mock<IMemberDirectory>();
        members.Setup(m => m.FindByAccountAsync(User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(Person, User, "Тарас", "Чупринка", "t@x", null));
        memberships.Setup(m => m.GetPeopleInKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var offices = new Mock<IOfficeDirectory>();
        offices.Setup(o => o.GetForAccountInKurinAsync(User, Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi)]);
        var scopes = new AgendaViewerDirectory(new CurrentMember(_user.Object, members.Object, memberships.Object), memberships.Object, members.Object, offices.Object, Mock.Of<IKurinTagDirectory>(), Mock.Of<IResourceScopeReader>());

        var permissions = new Mock<IPermissionResolver>();
        permissions.Setup(p => p.ResolveAsync(It.IsAny<IEnumerable<string>>(), Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _permissions);

        var kurins = KurinDirectories.Over(_unitOfWork.Object);
        var agenda = Agendas.Over(_unitOfWork.Object, memberships.Object);
        var books = new ScoreBookReader(_score.Object, agenda, kurins, memberships.Object, members.Object, Mock.Of<IScoreFactReader>(), _time);

        // Registered against the order the list comes in: the collector, not the registration, orders it.
        IDutySource[] sources =
        [
            new AttendanceDutyReader(books, agenda, Mock.Of<IKraiProfileDirectory>(), kurins, _time),
            new DuesDutyReader(_dues.Object, kurins),
            new BadgeDutyReader(memberships.Object, _progress.Object),
        ];

        return new GetMyDutiesQueryHandler(scopes, sources, permissions.Object, _unitOfWork.Object, _user.Object);
    }

    private async Task<MyDutiesDto> ReadAsync() =>
        (await Handler().Handle(new GetMyDutiesQuery(), CancellationToken.None)).Data!;

    private async Task<IReadOnlyList<MyDutyDto>> DutiesAsync() => (await ReadAsync()).Items;

    /// <summary>Someone who may also confirm вмілості, with <paramref name="count"/> of them waiting.</summary>
    private void ConfirmsBadges(int count)
    {
        _permissions.Add(new(ResourceType.BadgeProgress, ResourceAction.Update, AccessScope.KurinWide));
        _submittedBadges = count;
    }

    /// <summary>A скарбник too, of the kurin's box and of every гурток's.</summary>
    private void KeepsTheBoxes()
    {
        _permissions.Add(new(ResourceType.KurinDues, ResourceAction.Update, AccessScope.KurinWide));
        _permissions.Add(new(ResourceType.GroupDues, ResourceAction.Update, AccessScope.KurinWide));
    }

    /// <summary>
    /// Every kind at once, collected from the modules' sources, is the list «Справи» read before the
    /// sources existed: the same items, in the same order, with the same keys and fingerprints.
    /// </summary>
    [Fact]
    public async Task EveryKindAtOnce_ComesInTheOrderAndShapeItAlwaysHad()
    {
        ConfirmsBadges(3);
        KeepsTheBoxes();
        _duesEntries.AddRange(
        [
            new DuesEntry { KurinKey = Kurin, GroupKey = Bobry, Kind = DuesEntryKind.TransferToKurin, VerifiedAtUtc = Start },
            new DuesEntry { KurinKey = Kurin, GroupKey = Bobry, Kind = DuesEntryKind.TransferToKurin, VerifiedAtUtc = Start },
            new DuesEntry { KurinKey = Kurin, Kind = DuesEntryKind.Contribution },
            new DuesEntry { KurinKey = Kurin, GroupKey = Bobry, Kind = DuesEntryKind.Contribution },
            new DuesEntry { KurinKey = Kurin, GroupKey = Bobry, Kind = DuesEntryKind.Contribution },
        ]);

        var read = await ReadAsync();

        var kurin = MyKurinRefDto.Of(Standing, Kurin);
        read.HiddenCount.Should().Be(0);
        read.Items.Should().BeEquivalentTo(
            new[]
            {
                new MyDutyDto { Kind = MyDutyKind.BadgesToReview, Kurin = kurin, Count = 3, Key = $"BadgesToReview:{Kurin:N}:-:-:-", Fingerprint = "3" },
                new MyDutyDto { Kind = MyDutyKind.TransfersToConfirm, Kurin = kurin, Count = 2, Key = $"TransfersToConfirm:{Kurin:N}:-:-:-", Fingerprint = "2" },
                new MyDutyDto { Kind = MyDutyKind.EntriesToVerify, Kurin = kurin, Count = 1, Key = $"EntriesToVerify:{Kurin:N}:-:-:-", Fingerprint = "1" },
                new MyDutyDto
                {
                    Kind = MyDutyKind.EntriesToVerify, Kurin = kurin, Count = 2, GroupKey = Bobry, GroupName = "Бобри",
                    Key = $"EntriesToVerify:{Kurin:N}:{Bobry:N}:-:-", Fingerprint = "2"
                },
                new MyDutyDto
                {
                    Kind = MyDutyKind.EventWithoutAttendance, Kurin = kurin, Count = 1, AgendaItemKey = Skhodyny, OccurrenceStartUtc = Start,
                    Title = "Сходини", Key = $"EventWithoutAttendance:{Kurin:N}:-:{Skhodyny:N}:20261006T160000Z", Fingerprint = "1"
                },
            },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task AnUnmarkedMeetingWorthPoints_WaitsOnHim()
    {
        var duty = (await DutiesAsync()).Should().ContainSingle().Subject;

        duty.Should().BeEquivalentTo(new { Kind = MyDutyKind.EventWithoutAttendance, AgendaItemKey = (Guid?)Skhodyny, OccurrenceStartUtc = (DateTime?)Start, Title = "Сходини" });
    }

    [Fact]
    public async Task AMarkedMeeting_IsDone()
    {
        _marks.Add(new ScoreAttendance { KurinKey = Kurin, AgendaItemKey = Skhodyny, OccurrenceStartUtc = Start });

        (await DutiesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AMeetingThatDidNotTakePlace_IsNotADuty()
    {
        _notHeld.Add(new ScoreNotHeldOccurrence { KurinKey = Kurin, AgendaItemKey = Skhodyny, OccurrenceStartUtc = Start });

        (await DutiesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task EveryDuty_IsNamed_TheSameOnEveryRead()
    {
        ConfirmsBadges(3);

        var first = await DutiesAsync();
        var second = await DutiesAsync();

        first.Select(d => d.Key).Should().Equal(second.Select(d => d.Key));
        first.Select(d => d.Key).Should().OnlyHaveUniqueItems();
        first.Single(d => d.Kind == MyDutyKind.EventWithoutAttendance).Key
            .Should().Be($"EventWithoutAttendance:{Kurin:N}:-:{Skhodyny:N}:20261006T160000Z");
        first.Single(d => d.Kind == MyDutyKind.BadgesToReview).Fingerprint.Should().Be("3");
    }

    [Fact]
    public async Task AHiddenDuty_StaysOff_AndIsCounted()
    {
        var meeting = (await DutiesAsync()).Single();
        _dismissed.Add(new DismissedDuty { UserKey = User, DutyKey = meeting.Key, Fingerprint = meeting.Fingerprint });

        var read = await ReadAsync();

        read.Items.Should().BeEmpty();
        read.HiddenCount.Should().Be(1);
    }

    // Three вмілості hidden: one confirmed meanwhile keeps it hidden, one more brings it back.
    [Fact]
    public async Task AHiddenCount_ComesBack_OnlyWhenItGrows()
    {
        ConfirmsBadges(3);
        var badges = (await DutiesAsync()).Single(d => d.Kind == MyDutyKind.BadgesToReview);
        _dismissed.Add(new DismissedDuty { UserKey = User, DutyKey = badges.Key, Fingerprint = badges.Fingerprint });

        _submittedBadges = 2;
        (await DutiesAsync()).Should().NotContain(d => d.Kind == MyDutyKind.BadgesToReview);

        _submittedBadges = 4;
        var read = await ReadAsync();
        read.Items.Should().Contain(d => d.Kind == MyDutyKind.BadgesToReview && d.Count == 4);
        read.HiddenCount.Should().Be(0);
    }

    [Fact]
    public async Task AHiddenDutyThatIsDone_IsNotCounted()
    {
        var meeting = (await DutiesAsync()).Single();
        _dismissed.Add(new DismissedDuty { UserKey = User, DutyKey = meeting.Key, Fingerprint = meeting.Fingerprint });
        _marks.Add(new ScoreAttendance { KurinKey = Kurin, AgendaItemKey = Skhodyny, OccurrenceStartUtc = Start });

        (await ReadAsync()).HiddenCount.Should().Be(0);
    }
}
