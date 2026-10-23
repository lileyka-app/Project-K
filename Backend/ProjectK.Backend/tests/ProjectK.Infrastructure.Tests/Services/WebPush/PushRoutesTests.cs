using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Infrastructure.Services.WebPush;

namespace ProjectK.Infrastructure.Tests.Services.WebPush;

/// <summary>The same table as the PWA's notification-route.spec.ts, with /m/ in front and the inbox for the rest.</summary>
public class PushRoutesTests
{
    private static readonly Guid Kurin = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Item = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static AppNotificationDto Notification(
        AppNotificationType type,
        string? route,
        string? entityType = null,
        Guid? entityKey = null) => new()
    {
        Type = type,
        Route = route,
        EntityType = entityType,
        EntityKey = entityKey
    };

    [Fact]
    public void Member_ShouldOpenTheMembersCard()
    {
        var url = PushRoutes.For(Notification(AppNotificationType.MemberAwardReviewed, "/member/m-7?tab=awards"), null);

        Assert.Equal("/m/tabs/kurin/member/m-7", url);
    }

    [Fact]
    public void Review_ShouldOpenTheQueueOfTheKurinTheAppIsIn()
    {
        var notification = Notification(AppNotificationType.MemberSkillSubmittedForReview, $"/kurin/{Kurin}/review/skills");

        Assert.Equal("/m/tabs/kurin/review/skills", PushRoutes.For(notification, Kurin));
        Assert.Equal("/m/tabs/kurin/review/skills", PushRoutes.For(notification, null));
        Assert.Equal(PushRoutes.Inbox, PushRoutes.For(notification, Guid.NewGuid()));
        Assert.True(PushRoutes.NeedsActiveKurin(notification));
    }

    [Theory]
    [InlineData("/tasks/k1", AppNotificationType.AgendaItemAssigned, "/m/tabs/tasks/task/22222222-2222-2222-2222-222222222222")]
    [InlineData("/calendar/k1", AppNotificationType.AgendaItemUpdated, "/m/tabs/calendar/event/22222222-2222-2222-2222-222222222222")]
    [InlineData("/tasks/k1", AppNotificationType.AgendaItemDeleted, "/m/tabs/tasks")]
    [InlineData("/calendar/k1", AppNotificationType.AgendaItemDeleted, "/m/tabs/calendar")]
    public void Agenda_ShouldOpenTheItemOrItsListWhenItIsGone(string route, AppNotificationType type, string expected)
    {
        var url = PushRoutes.For(Notification(type, route, "AgendaItem", Item), null);

        Assert.Equal(expected, url);
    }

    [Fact]
    public void ACalendarLinkGoneOff_ShouldOpenTheAccountPage()
    {
        var url = PushRoutes.For(Notification(AppNotificationType.CalendarFeedRevoked, "/settings/account#calendar"), null);

        Assert.Equal("/m/tabs/more/account", url);
    }

    [Theory]
    [InlineData("/waitlist")]
    [InlineData(null)]
    [InlineData("/somewhere/else")]
    public void Anything_ShouldOpenTheInboxWhenThePhoneHasNoScreenForIt(string? route)
    {
        var notification = Notification(AppNotificationType.WaitlistEntrySubmitted, route);

        Assert.Equal("/m/tabs/home/notifications", PushRoutes.For(notification, null));
        Assert.False(PushRoutes.NeedsActiveKurin(notification));
    }
}
