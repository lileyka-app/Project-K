using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.InfrastructureModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Rules.AuthModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Events;

/// <summary>
/// Who hears about a request between kurins. It goes to the Звʼязковий of a kurin and nobody else in
/// its провід: only that office answers or sends requests, and a kurin always has one.
/// </summary>
internal static class MembershipRequestNotices
{
    public const string Route = "/kurin/registry/transfers";

    /// <summary>Where a person answers a kurin's invitation themself.</summary>
    public const string PersonRoute = "/invitations";

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
        var asking = await MembershipRequestNotices.KurinAsync(_unitOfWork, raised.ToKurinKey, cancellationToken);
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
                    Route = MembershipRequestNotices.PersonRoute,
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
            (await MembershipRequestNotices.ZvyazkoviAsync(_unitOfWork, fromKurinKey, cancellationToken))
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
        var answeredByPerson = raised.FromKurinKey is null;
        var from = raised.FromKurinKey is { } fromKurinKey
            ? await MembershipRequestNotices.KurinAsync(_unitOfWork, fromKurinKey, cancellationToken)
            : raised.MemberName;
        var to = await MembershipRequestNotices.KurinAsync(_unitOfWork, raised.ToKurinKey, cancellationToken);

        var requests = (await MembershipRequestNotices.ZvyazkoviAsync(_unitOfWork, raised.ToKurinKey, cancellationToken))
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
                Route = MembershipRequestNotices.Route,
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
