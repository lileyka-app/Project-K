using MediatR;
using Microsoft.Extensions.Logging;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.NotificationsModule;
using ProjectK.Common.Enums.NotificationsModule;
using ProjectK.Common.Events.KurinModule;

namespace ProjectK.BusinessLogic.Modules.NotificationsModule.Events;

/// <summary>
/// The one who sent the request hears how it was settled. Runs after the decision is saved, so a
/// failed notification must not turn an applied decision into an error for the administrator.
/// </summary>
public sealed class KurinBranchRequestReviewedEventHandler
    : INotificationHandler<DomainEventNotification<KurinBranchRequestReviewed>>
{
    private readonly INotificationService _notifications;
    private readonly ILogger<KurinBranchRequestReviewedEventHandler> _logger;

    public KurinBranchRequestReviewedEventHandler(INotificationService notifications, ILogger<KurinBranchRequestReviewedEventHandler> logger)
    {
        _notifications = notifications;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<KurinBranchRequestReviewed> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        try
        {
            await Notify(raised, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Kurin branch request {RequestKey} settled, but the requester could not be told", raised.KurinBranchRequestKey);
        }
    }

    private Task Notify(KurinBranchRequestReviewed raised, CancellationToken cancellationToken)
        => _notifications.NotifyAsync(
            new NotificationRequest
            {
                RecipientUserKey = raised.RequesterUserKey,
                Type = AppNotificationType.KurinBranchRequestReviewed,
                Severity = raised.Approved ? AppNotificationSeverity.Success : AppNotificationSeverity.Info,
                Title = raised.Approved ? "Гілки куреня змінено" : "Запит на зміну гілок відхилено",
                Body = raised.Branches,
                EntityType = "KurinBranchRequest",
                EntityKey = raised.KurinBranchRequestKey,
                Route = $"/kurin/{raised.KurinKey}/settings",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"kurin-branch-request-reviewed:{raised.KurinBranchRequestKey}"
            },
            cancellationToken);
}
