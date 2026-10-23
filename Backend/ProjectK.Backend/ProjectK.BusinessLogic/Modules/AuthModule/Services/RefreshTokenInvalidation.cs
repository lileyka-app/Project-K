using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <summary>
/// Ends every session an account holds, and switches off its calendar link.
/// <para>
/// Used by the changes that must not leave an older session alive: a new password, a confirmed
/// email change, anything touching the second factor, a suspension. It used to be one line clearing
/// the single token column; now that an account can be signed in in several places, "revoke" has to
/// mean all of them, or a change made because of a suspected compromise would leave the intruder
/// logged in. The calendar link is access too — an intruder could have issued one — so it goes with
/// them, and the person is told, since a subscription that stops updating says nothing by itself.
/// </para>
/// <para>
/// Call it only once the change is stored: both stores commit on their own, and so does the
/// notification.
/// </para>
/// </summary>
public class RefreshTokenInvalidation
{
    public const string CalendarRoute = "/settings/account#calendar";

    private readonly IRefreshTokenStore _refreshTokens;
    private readonly ICalendarFeedStore _calendarFeeds;
    private readonly INotificationService _notifications;

    public RefreshTokenInvalidation(IRefreshTokenStore refreshTokens, ICalendarFeedStore calendarFeeds, INotificationService notifications)
    {
        _refreshTokens = refreshTokens;
        _calendarFeeds = calendarFeeds;
        _notifications = notifications;
    }

    public Task RevokeRefreshTokenAsync(AppUser user, CancellationToken cancellationToken = default) =>
        RevokeRefreshTokenAsync(user, CalendarFeedRevokeReason.SecurityChange, cancellationToken);

    public async Task RevokeRefreshTokenAsync(AppUser user, CalendarFeedRevokeReason reason, CancellationToken cancellationToken = default)
    {
        await _refreshTokens.RevokeAllAsync(user.Id, cancellationToken);
        await RevokeCalendarFeedAsync(user.Id, reason, cancellationToken);
    }

    /// <summary>The calendar link alone, for a change that does not end sessions.</summary>
    public async Task RevokeCalendarFeedAsync(Guid userId, CalendarFeedRevokeReason reason, CancellationToken cancellationToken = default)
    {
        if (!await _calendarFeeds.RevokeActiveAsync(userId, reason, cancellationToken))
        {
            return;
        }

        await _notifications.NotifyAsync(new NotificationRequest
        {
            RecipientUserKey = userId,
            Type = AppNotificationType.CalendarFeedRevoked,
            Severity = AppNotificationSeverity.Warn,
            Title = "Посилання на календар вимкнено",
            Body = "Після зміни в безпеці акаунта старе посилання більше не оновлює твій календар. Створи нове в налаштуваннях і додай його в календар знову.",
            Route = CalendarRoute,
            DeduplicationKey = $"calendar-feed-revoked:{userId}"
        }, cancellationToken);
    }
}
