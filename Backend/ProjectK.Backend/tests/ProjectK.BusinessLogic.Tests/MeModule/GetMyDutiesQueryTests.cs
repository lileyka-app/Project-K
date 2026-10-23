using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Duties;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.MeModule;

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

    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
    private readonly List<Permission> _permissions = [new(ResourceType.GroupScore, ResourceAction.Create, AccessScope.KurinWide)];
    private readonly List<ScoreAttendance> _marks = [];
    private readonly List<ScoreNotHeldOccurrence> _notHeld = [];
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IScoreUnitOfWork> _score = new();
    private readonly Mock<ICurrentUserContext> _user = new();
    private readonly List<DismissedDuty> _dismissed = [];
    private readonly Mock<IMemberProgressDirectory> _progress = new();
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
        var dismissed = new Mock<IDismissedDutyRepository>();
        dismissed.Setup(d => d.GetForUserAsync(User, It.IsAny<CancellationToken>())).ReturnsAsync(() => _dismissed);
        _unitOfWork.SetupGet(u => u.DismissedDuties).Returns(dismissed.Object);
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
            .ReturnsAsync([new MembershipRecord(Guid.NewGuid(), Kurin, 1, KurinBranch.UPYu, null, null, null, MembershipKind.Staff, Start.AddYears(-1), null)]);
        var members = new Mock<IMemberDirectory>();
        members.Setup(m => m.FindByAccountAsync(User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(Person, User, Kurin, null, "Тарас", "Чупринка", "t@x", null));
        members.Setup(m => m.GetByKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var offices = new Mock<IOfficeDirectory>();
        offices.Setup(o => o.GetForAccountInKurinAsync(User, Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi)]);
        var scopes = new MeAgendaScopes(_user.Object, memberships.Object, members.Object, offices.Object, Mock.Of<IResourceScopeReader>(), _unitOfWork.Object);

        var permissions = new Mock<IPermissionResolver>();
        permissions.Setup(p => p.ResolveAsync(It.IsAny<IEnumerable<string>>(), Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _permissions);

        var books = new ScoreBookReader(_score.Object, _unitOfWork.Object, memberships.Object, members.Object, Mock.Of<IScoreFactSource>(), _time);

        return new GetMyDutiesQueryHandler(
            scopes, _unitOfWork.Object, Mock.Of<IDuesUnitOfWork>(), members.Object, _progress.Object,
            books, _user.Object, _time, Mock.Of<IKraiProfileProvider>(), permissions.Object);
    }

    private async Task<MyDutiesResponse> ReadAsync() =>
        (await Handler().Handle(new GetMyDutiesQuery(), CancellationToken.None)).Data!;

    private async Task<IReadOnlyList<MyDutyDto>> DutiesAsync() => (await ReadAsync()).Items;

    /// <summary>Someone who may also confirm вмілості, with <paramref name="count"/> of them waiting.</summary>
    private void ConfirmsBadges(int count)
    {
        _permissions.Add(new(ResourceType.BadgeProgress, ResourceAction.Update, AccessScope.KurinWide));
        _submittedBadges = count;
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
