using System.Text.RegularExpressions;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Infrastructure.Services.WebPush;

/// <summary>
/// Where a tap on a push opens the PWA (served under <c>/m/</c>). The notification's <c>Route</c> is
/// the web's path; this is the phone's mapping of it, kept in step with
/// <c>mobile/src/app/features/leader/notification-route.ts</c>, which does the same for a tap in the
/// in-app inbox. Anything the phone has no screen for opens that inbox instead of nothing.
/// </summary>
public static partial class PushRoutes
{
    public const string Base = "/m/";
    public const string Inbox = Base + "tabs/home/notifications";

    private static readonly char[] PathEnd = ['?', '#'];

    /// <param name="notification">The saved notification.</param>
    /// <param name="activeKurinKey">
    /// The kurin the recipient's app is in (<c>null</c> when unknown). The review queue opens for the
    /// app's kurin, so another kurin's queue goes to the inbox instead of showing the wrong one.
    /// </param>
    public static string For(AppNotificationDto notification, Guid? activeKurinKey)
    {
        var route = PathOf(notification);

        var member = MemberRoute().Match(route);
        if (member.Success)
        {
            return $"{Base}tabs/kurin/member/{member.Groups[1].Value}";
        }

        var review = ReviewRoute().Match(route);
        if (review.Success)
        {
            var sameKurin = activeKurinKey is null
                || string.Equals(review.Groups[1].Value, activeKurinKey.Value.ToString(), StringComparison.OrdinalIgnoreCase);
            return sameKurin ? $"{Base}tabs/kurin/review/skills" : Inbox;
        }

        var agenda = AgendaRoute().Match(route);
        return agenda.Success ? AgendaTarget(agenda, notification) : Inbox;
    }

    private static string AgendaTarget(Match agenda, AppNotificationDto notification)
    {
        var isTask = agenda.Groups[1].Value == "tasks";
        var item = notification.EntityType == "AgendaItem" ? notification.EntityKey : null;
        // A deleted item has no page left; its list is the nearest place.
        if (item is null || notification.Type == AppNotificationType.AgendaItemDeleted)
        {
            return isTask ? $"{Base}tabs/tasks" : $"{Base}tabs/calendar";
        }

        return isTask ? $"{Base}tabs/tasks/task/{item}" : $"{Base}tabs/calendar/event/{item}";
    }

    /// <summary>Whether the route is a review queue: only then does the recipient's kurin matter.</summary>
    public static bool NeedsActiveKurin(AppNotificationDto notification) =>
        ReviewRoute().IsMatch(PathOf(notification));

    private static string PathOf(AppNotificationDto notification) =>
        (notification.Route ?? string.Empty).Split(PathEnd)[0];

    [GeneratedRegex("^/member/([^/]+)/?$")]
    private static partial Regex MemberRoute();

    [GeneratedRegex("^/kurin/([^/]+)/review/skills/?$")]
    private static partial Regex ReviewRoute();

    [GeneratedRegex("^/(tasks|calendar)(/|$)")]
    private static partial Regex AgendaRoute();
}
