using MediatR;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Services;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Enums.InfrastructureModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Events.MemberModule;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Events;

/// <summary>A new request reaches whoever answers it: the Звʼязковий of the person's kurin, or the person.</summary>
public sealed class MembershipRequestedEventHandler : INotificationHandler<DomainEventNotification<MembershipRequested>>
{
    private readonly INotificationService _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public MembershipRequestedEventHandler(INotificationService notifications, IUnitOfWork unitOfWork)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DomainEventNotification<MembershipRequested> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        var asking = await MembershipRequestNoticeRules.KurinAsync(_unitOfWork, raised.ToKurinKey, cancellationToken);
        if (raised.FromKurinKey is not { } fromKurinKey)
        {
            if (raised.MemberUserKey is { } person)
            {
                await _notifications.NotifyAsync(new NotificationRequest
                {
                    RecipientUserKey = person,
                    Type = AppNotificationType.MembershipRequested,
                    Severity = AppNotificationSeverity.Warn,
                    Title = "Запрошення до куреня",
                    Body = $"{asking} запрошує тебе до себе.",
                    EntityType = "MembershipRequest",
                    EntityKey = raised.MembershipRequestKey,
                    Route = MembershipRequestNoticeRules.PersonRoute,
                    ActorUserKey = raised.ActorUserKey,
                    DeduplicationKey = $"membership-request:{raised.MembershipRequestKey}"
                }, cancellationToken);
            }

            return;
        }

        var body = raised.Kind == MembershipRequestKind.Transfer
            ? $"{asking} просить перевести до себе: {raised.MemberName}."
            : $"{asking} просить прийняти ще й до себе: {raised.MemberName}.";

        await _notifications.NotifyManyAsync(
            (await MembershipRequestNoticeRules.ZvyazkoviAsync(_unitOfWork, fromKurinKey, cancellationToken))
            .Select(userKey => new NotificationRequest
            {
                RecipientUserKey = userKey,
                Type = AppNotificationType.MembershipRequested,
                Severity = AppNotificationSeverity.Warn,
                Title = "Запит на перехід",
                Body = body,
                EntityType = "MembershipRequest",
                EntityKey = raised.MembershipRequestKey,
                Route = MembershipRequestNoticeRules.Route,
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"membership-request:{raised.MembershipRequestKey}"
            }),
            cancellationToken);
    }
}
