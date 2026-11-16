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

/// <summary>
/// An answer reaches the asking kurin's Звʼязковий, and — when approved — the person, who now stands
/// somewhere new.
/// </summary>
public sealed class MembershipRequestDecidedEventHandler : INotificationHandler<DomainEventNotification<MembershipRequestDecided>>
{
    private readonly INotificationService _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public MembershipRequestDecidedEventHandler(INotificationService notifications, IUnitOfWork unitOfWork)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DomainEventNotification<MembershipRequestDecided> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        var answeredByPerson = raised.FromKurinKey is null;
        var from = raised.FromKurinKey is { } fromKurinKey
            ? await MembershipRequestNoticeRules.KurinAsync(_unitOfWork, fromKurinKey, cancellationToken)
            : raised.MemberName;
        var to = await MembershipRequestNoticeRules.KurinAsync(_unitOfWork, raised.ToKurinKey, cancellationToken);

        var requests = (await MembershipRequestNoticeRules.ZvyazkoviAsync(_unitOfWork, raised.ToKurinKey, cancellationToken))
            .Select(userKey => new NotificationRequest
            {
                RecipientUserKey = userKey,
                Type = AppNotificationType.MembershipRequestDecided,
                Severity = raised.Approved ? AppNotificationSeverity.Success : AppNotificationSeverity.Info,
                Title = raised.Approved ? "Запит підтверджено" : "Запит відхилено",
                Body = answeredByPerson
                    ? raised.Approved
                        ? $"{raised.MemberName} прийняв запрошення і тепер у вашому курені."
                        : $"{raised.MemberName} відхилив запрошення."
                    : raised.Approved
                        ? $"{from} підтвердив: {raised.MemberName} тепер у вашому курені."
                        : $"{from} відхилив запит: {raised.MemberName} лишається там.",
                EntityType = "MembershipRequest",
                EntityKey = raised.MembershipRequestKey,
                Route = MembershipRequestNoticeRules.Route,
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"membership-request-decided:{raised.MembershipRequestKey}"
            })
            .ToList();

        // Whoever answered for themself already knows.
        if (raised.Approved && !answeredByPerson && raised.MemberUserKey is { } person)
        {
            requests.Add(new NotificationRequest
            {
                RecipientUserKey = person,
                Type = AppNotificationType.MembershipRequestDecided,
                Severity = AppNotificationSeverity.Success,
                Title = raised.Kind == MembershipRequestKind.Transfer ? "Тебе переведено" : "Тебе прийнято ще й до куреня",
                Body = raised.Kind == MembershipRequestKind.Transfer
                    ? $"Тепер ти в {to}. Усе здобуте лишається з тобою."
                    : $"Ти тепер і в {to}, і далі в {from}.",
                EntityType = "MembershipRequest",
                EntityKey = raised.MembershipRequestKey,
                Route = "/",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"membership-request-person:{raised.MembershipRequestKey}"
            });
        }

        await _notifications.NotifyManyAsync(requests, cancellationToken);
    }
}
