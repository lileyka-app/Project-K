using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Notifications;

/// <summary>
/// Who hears about a request between kurins. It goes to the Звʼязковий of a kurin and nobody else in
/// its провід: only that office answers or sends requests, and a kurin always has one.
/// </summary>
internal static class MembershipRequestNotices
{
    public const string Route = "/kurin/registry/transfers";

    public static async Task<IReadOnlyCollection<Guid>> ZvyazkoviAsync(IUnitOfWork unitOfWork, Guid kurinKey, CancellationToken cancellationToken)
    {
        var role = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi);
        return (await unitOfWork.Leaderships.GetOfficeMembersLookupAsync(kurinKey, LeadershipType.KV, cancellationToken))
            .Where(holder => holder.UserKey.HasValue && holder.UserRole == role)
            .Select(holder => holder.UserKey!.Value)
            .Distinct()
            .ToList();
    }

    public static async Task<string> KurinAsync(IUnitOfWork unitOfWork, Guid kurinKey, CancellationToken cancellationToken)
    {
        var kurin = await unitOfWork.Kurins.GetByKeyAsync(kurinKey, cancellationToken);
        return kurin is null ? "інший курінь" : KurinBranchNames.InText(kurin);
    }
}

/// <summary>A new request reaches the Звʼязковий who answers it.</summary>
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
        var asking = await MembershipRequestNotices.KurinAsync(_unitOfWork, raised.ToKurinKey, cancellationToken);
        var body = raised.Kind == MembershipRequestKind.Transfer
            ? $"{asking} просить перевести до себе: {raised.MemberName}."
            : $"{asking} просить прийняти ще й до себе: {raised.MemberName}.";

        await _notifications.NotifyManyAsync(
            (await MembershipRequestNotices.ZvyazkoviAsync(_unitOfWork, raised.FromKurinKey, cancellationToken))
            .Select(userKey => new NotificationRequest
            {
                RecipientUserKey = userKey,
                Type = AppNotificationType.MembershipRequested,
                Severity = AppNotificationSeverity.Warn,
                Title = "Запит на перехід",
                Body = body,
                EntityType = "MembershipRequest",
                EntityKey = raised.MembershipRequestKey,
                Route = MembershipRequestNotices.Route,
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"membership-request:{raised.MembershipRequestKey}"
            }),
            cancellationToken);
    }
}

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
        var from = await MembershipRequestNotices.KurinAsync(_unitOfWork, raised.FromKurinKey, cancellationToken);
        var to = await MembershipRequestNotices.KurinAsync(_unitOfWork, raised.ToKurinKey, cancellationToken);

        var requests = (await MembershipRequestNotices.ZvyazkoviAsync(_unitOfWork, raised.ToKurinKey, cancellationToken))
            .Select(userKey => new NotificationRequest
            {
                RecipientUserKey = userKey,
                Type = AppNotificationType.MembershipRequestDecided,
                Severity = raised.Approved ? AppNotificationSeverity.Success : AppNotificationSeverity.Info,
                Title = raised.Approved ? "Запит підтверджено" : "Запит відхилено",
                Body = raised.Approved
                    ? $"{from} підтвердив: {raised.MemberName} тепер у вашому курені."
                    : $"{from} відхилив запит: {raised.MemberName} лишається там.",
                EntityType = "MembershipRequest",
                EntityKey = raised.MembershipRequestKey,
                Route = MembershipRequestNotices.Route,
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"membership-request-decided:{raised.MembershipRequestKey}"
            })
            .ToList();

        if (raised.Approved && raised.MemberUserKey is { } person)
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
