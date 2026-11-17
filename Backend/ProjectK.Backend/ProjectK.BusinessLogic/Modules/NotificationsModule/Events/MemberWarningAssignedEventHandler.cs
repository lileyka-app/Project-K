using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.NotificationsModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.NotificationsModule;
using ProjectK.Common.Events.MemberModule;

namespace ProjectK.BusinessLogic.Modules.NotificationsModule.Events;

public sealed class MemberWarningAssignedEventHandler
    : INotificationHandler<DomainEventNotification<MemberWarningAssigned>>
{
    private readonly INotificationService _notifications;

    public MemberWarningAssignedEventHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public Task Handle(DomainEventNotification<MemberWarningAssigned> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        return _notifications.NotifyAsync(
            new NotificationRequest
            {
                RecipientUserKey = raised.MemberUserKey,
                Type = AppNotificationType.MemberWarningAssigned,
                Severity = AppNotificationSeverity.Warn,
                Title = "Пересторогу призначено",
                Body = $"До вашого профілю додано {WarningLevelName(raised.Level)}.",
                EntityType = "MemberWarning",
                EntityKey = raised.MemberWarningKey,
                Route = $"/member/{raised.MemberKey}",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"member-warning:{raised.MemberWarningKey}"
            },
            cancellationToken);
    }

    private static string WarningLevelName(MemberWarningLevel level) =>
        level switch
        {
            MemberWarningLevel.Level1 => "першу пересторогу",
            MemberWarningLevel.Level2 => "другу пересторогу",
            MemberWarningLevel.Level3 => "третю пересторогу",
            _ => "пересторогу"
        };
}
