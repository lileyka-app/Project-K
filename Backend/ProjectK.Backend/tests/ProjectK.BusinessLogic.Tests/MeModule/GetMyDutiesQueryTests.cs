using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Duties;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
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
        var offices = new Mock<IOfficeDirectory>();
        offices.Setup(o => o.GetForAccountInKurinAsync(User, Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi)]);
        var scopes = new MeAgendaScopes(_user.Object, memberships.Object, members.Object, offices.Object, Mock.Of<IResourceScopeReader>(), _unitOfWork.Object);

        var permissions = new Mock<IPermissionResolver>();
        permissions.Setup(p => p.ResolveAsync(It.IsAny<IEnumerable<string>>(), Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _permissions);

        var books = new ScoreBookReader(_score.Object, _unitOfWork.Object, memberships.Object, members.Object, Mock.Of<IScoreFactSource>(), _time);

        return new GetMyDutiesQueryHandler(
            scopes, _unitOfWork.Object, Mock.Of<IDuesUnitOfWork>(), members.Object, Mock.Of<IMemberProgressDirectory>(),
            books, _user.Object, _time, Mock.Of<IKraiProfileProvider>(), permissions.Object);
    }

    private async Task<IReadOnlyList<MyDutyDto>> DutiesAsync() =>
        (await Handler().Handle(new GetMyDutiesQuery(), CancellationToken.None)).Data!;

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
}
