using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.MeModule.Features.CalendarFeed.Render;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;
using ProjectK.Common.Security;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.MeModule;

/// <summary>
/// What the calendar link hands to Google: the person's own events, the options they chose, and a
/// notice instead of silence once the link is off.
/// </summary>
public class CalendarFeedTests
{
    private const string Token = "secret-token";
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Person = Guid.NewGuid();
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Sokoly = Guid.NewGuid();
    private static readonly Guid Kelty = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private static readonly AgendaCategory Skhodyny = new()
    {
        AgendaCategoryKey = Guid.NewGuid(), KurinKey = Kurin, Name = "Сходини", IsKurinSchedule = true
    };

    private readonly Mock<ICalendarFeedStore> _feeds = new();
    private readonly Mock<IOfficeDirectory> _offices = new();
    private readonly List<AgendaItem> _items = [];
    private CalendarFeed _feed = new() { UserId = User, TokenHash = SecretHash.Sha256Hex(Token) };
    private OnboardingStatus _status = OnboardingStatus.Active;

    public CalendarFeedTests()
    {
        _feeds.Setup(f => f.FindByTokenHashAsync(SecretHash.Sha256Hex(Token), It.IsAny<CancellationToken>())).ReturnsAsync(() => _feed);
        _offices.Setup(o => o.GetForAccountInKurinAsync(User, Kurin, It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    private void AsZvyazkovyi() =>
        _offices.Setup(o => o.GetForAccountInKurinAsync(User, Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi)]);

    private RenderCalendarFeedQueryHandler Handler()
    {
        var currentUser = new Mock<ICurrentUserContext>();
        var memberships = new Mock<IMembershipDirectory>();
        memberships.Setup(m => m.GetCurrentForAccountAsync(User, It.IsAny<CancellationToken>())).ReturnsAsync(
            [new MembershipRecord(Guid.NewGuid(), Kurin, 12, KurinBranch.UPYu, null, Sokoly, "Соколи", MembershipKind.Youth, DateTime.UtcNow, null)]);
        var members = new Mock<IMemberDirectory>();
        members.Setup(m => m.FindByAccountAsync(User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(Person, User, Kurin, Sokoly, "Оксана", "Паливода", "o@x", null));
        var scopeReader = new Mock<IResourceScopeReader>();
        scopeReader.Setup(r => r.GetLedGroupKeysAsync(User, Kurin, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var leaderships = new Mock<ILeadershipRepository>();
        leaderships.Setup(l => l.GetActiveLeadershipKeysForMemberAsync(Person, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var agenda = new Mock<IAgendaItemRepository>();
        agenda.Setup(a => a.GetForViewerAsync(It.IsAny<AgendaViewerScope>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), true,
                It.IsAny<AgendaItemKind?>(), It.IsAny<CancellationToken>(), false, It.IsAny<bool>()))
            .ReturnsAsync((AgendaViewerScope _, DateTime? _, DateTime? _, bool _, AgendaItemKind? kind, CancellationToken _, bool _, bool _) =>
                _items.Where(i => kind is null || i.Kind == kind).ToList());
        var categories = new Mock<IAgendaCategoryRepository>();
        categories.Setup(c => c.GetForKurinAsync(Kurin, true, It.IsAny<CancellationToken>())).ReturnsAsync([Skhodyny]);
        var groups = new Mock<IGroupRepository>();
        groups.Setup(g => g.GetAllAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(
            [new Group("Соколи", Kurin) { GroupKey = Sokoly }, new Group("Кельти", Kurin) { GroupKey = Kelty }]);
        var responses = new Mock<IAgendaResponseRepository>();
        responses.Setup(r => r.GetForUserAsync(User, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Leaderships).Returns(leaderships.Object);
        unitOfWork.SetupGet(u => u.AgendaItems).Returns(agenda.Object);
        unitOfWork.SetupGet(u => u.AgendaCategories).Returns(categories.Object);
        unitOfWork.SetupGet(u => u.Groups).Returns(groups.Object);
        unitOfWork.SetupGet(u => u.AgendaResponses).Returns(responses.Object);

        var scopes = new MeAgendaScopes(currentUser.Object, memberships.Object, members.Object, _offices.Object, scopeReader.Object, unitOfWork.Object);

        var userManager = new Mock<UserManager<AppUser>>(new Mock<IUserStore<AppUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.FindByIdAsync(User.ToString())).ReturnsAsync(() => new AppUser { Id = User, OnboardingStatus = _status });

        return new RenderCalendarFeedQueryHandler(
            _feeds.Object, userManager.Object, scopes, unitOfWork.Object, members.Object,
            new Mock<IKraiProfileProvider>().Object, new FixedTimeProvider(Now),
            Options.Create(new EmailSettings { BaseUrl = "https://lileyka.app" }));
    }

    private AgendaItem Event(string title, Guid group, Guid? category = null, Guid? createdBy = null)
    {
        var item = new AgendaItem
        {
            AgendaItemKey = Guid.NewGuid(),
            KurinKey = Kurin,
            Kind = AgendaItemKind.Event,
            Title = title,
            StartUtc = new DateTime(2026, 10, 22, 15, 30, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2026, 10, 22, 17, 0, 0, DateTimeKind.Utc),
            IsAllDay = false,
            AgendaCategoryKey = category,
            CreatedByUserKey = createdBy ?? Guid.NewGuid(),
            Assignments = [new AgendaAssignment { TargetType = AgendaTargetType.Group, TargetKey = group }]
        };
        _items.Add(item);
        return item;
    }

    private async Task<string[]> Render()
    {
        var result = await Handler().Handle(new RenderCalendarFeedQuery(Token), CancellationToken.None);
        result.Type.Should().Be(ResultType.Success);
        return result.Data!.Content.Replace("\r\n ", string.Empty, StringComparison.Ordinal).Split("\r\n");
    }

    private static IEnumerable<string> Summaries(string[] lines) =>
        lines.Where(l => l.StartsWith("SUMMARY:", StringComparison.Ordinal)).Select(l => l["SUMMARY:".Length..]);

    [Fact]
    public async Task AnUnknownToken_IsNotFound()
    {
        var result = await Handler().Handle(new RenderCalendarFeedQuery("nobody-issued-this"), CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
    }

    [Fact]
    public async Task OnesOwnEvents_AreThere_AndAnotherGurtoksAreNot()
    {
        Event("Сходини", Sokoly, Skhodyny.AgendaCategoryKey);
        Event("Мандрівка Кельтів", Kelty);

        Summaries(await Render()).Should().Equal("Сходини");
    }

    // A Звʼязковий sees every event of the kurin on the web; their own calendar gets their own.
    [Fact]
    public async Task AWholeKurinViewer_GetsOnlyWhatIsTheirs()
    {
        AsZvyazkovyi();
        Event("Мандрівка Кельтів", Kelty);
        Event("Вогнище", Kelty, createdBy: User);

        Summaries(await Render()).Should().Equal("Вогнище");
    }

    [Fact]
    public async Task OtherGurtoksSkhodyny_ComeOnlyWithTheOption_NamedAfterTheGurtok()
    {
        AsZvyazkovyi();
        Event("Сходини", Kelty, Skhodyny.AgendaCategoryKey);

        Summaries(await Render()).Should().BeEmpty();

        _feed = new CalendarFeed { UserId = User, TokenHash = SecretHash.Sha256Hex(Token), IncludeSchedules = true };
        Summaries(await Render()).Should().Equal("Кельти · Сходини");
    }

    // 18:30 in Kyiv stays 18:30 after the clocks go back on the last Sunday of October.
    [Fact]
    public async Task AWeeklySeries_KeepsItsWallClockTimeAcrossTheClockChange()
    {
        var item = Event("Сходини", Sokoly, Skhodyny.AgendaCategoryKey);
        item.RecurrenceFrequency = RecurrenceFrequency.Weekly;
        item.RecurrenceInterval = 1;
        item.RecurrenceByWeekday = 1 << (int)DayOfWeek.Thursday;
        item.RecurrenceCount = 3;

        var starts = (await Render()).Where(l => l.StartsWith("DTSTART:", StringComparison.Ordinal)).ToList();

        starts.Should().Equal("DTSTART:20261022T153000Z", "DTSTART:20261029T163000Z", "DTSTART:20261105T163000Z");
    }

    [Fact]
    public async Task ARevokedLink_AnswersWithANoticeInsteadOfEvents()
    {
        Event("Сходини", Sokoly, Skhodyny.AgendaCategoryKey);
        _feed = new CalendarFeed
        {
            UserId = User, TokenHash = SecretHash.Sha256Hex(Token),
            RevokedAtUtc = Now.UtcDateTime.AddDays(-2), RevokedReason = CalendarFeedRevokeReason.SecurityChange
        };

        var lines = await Render();

        Summaries(lines).Should().NotBeEmpty().And.OnlyContain(s => s == "Посилання на календар Лілейки вимкнено");
        lines.Should().Contain(l => l.Contains("після зміни пароля", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ALinkOfASuspendedAccount_AnswersWithTheNoticeToo()
    {
        Event("Сходини", Sokoly, Skhodyny.AgendaCategoryKey);
        _status = OnboardingStatus.Suspended;

        Summaries(await Render()).Should().OnlyContain(s => s == "Посилання на календар Лілейки вимкнено");
    }

    [Fact]
    public async Task ASecurityChange_RevokesTheLink_AndTellsThePerson()
    {
        var notifications = new Mock<INotificationService>();
        _feeds.Setup(f => f.RevokeActiveAsync(User, CalendarFeedRevokeReason.SecurityChange, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var sessions = Revocations.Of(new Mock<IRefreshTokenStore>().Object, _feeds.Object, notifications.Object);

        await sessions.RevokeRefreshTokenAsync(new AppUser { Id = User });

        notifications.Verify(n => n.NotifyAsync(
            It.Is<NotificationRequest>(r => r.RecipientUserKey == User && r.Type == AppNotificationType.CalendarFeedRevoked && r.Route == RefreshTokenInvalidation.CalendarRoute),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ASecurityChange_WithoutALink_NotifiesNobody()
    {
        var notifications = new Mock<INotificationService>();
        var sessions = Revocations.Of(new Mock<IRefreshTokenStore>().Object, _feeds.Object, notifications.Object);

        await sessions.RevokeRefreshTokenAsync(new AppUser { Id = User });

        notifications.Verify(n => n.NotifyAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
