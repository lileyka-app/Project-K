using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Enums.InfrastructureModule;
using ProjectK.Common.Events.MemberModule;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Events;

/// <summary>
/// Turns what happened to a member into something they read in their inbox. The wording, the
/// severity and the route live here rather than in the module that raised the event: the курінь does
/// not know there is an inbox, and this file is the only place that has to change when the wording does.
/// </summary>
/// <summary>
/// A stale verification goes to the провід who can verify again — the впорядники of the person's
/// гурток and the kurin-wide reviewers — in every kurin where the person stands and verification is on.
/// The person used to get it, and could do nothing with it.
/// </summary>
public sealed class MemberProfileWentStaleEventHandler
    : INotificationHandler<DomainEventNotification<MemberProfileWentStale>>
{
    private readonly INotificationService _notifications;
    private readonly IReviewNotificationRecipientReader _recipients;
    private readonly IUnitOfWork _unitOfWork;

    public MemberProfileWentStaleEventHandler(
        INotificationService notifications,
        IReviewNotificationRecipientReader recipients,
        IUnitOfWork unitOfWork)
    {
        _notifications = notifications;
        _recipients = recipients;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DomainEventNotification<MemberProfileWentStale> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        var recipientUserKeys = new HashSet<Guid>();

        foreach (var membership in await _unitOfWork.Memberships.GetActiveForMemberAsync(raised.MemberKey, cancellationToken))
        {
            var kurin = await _unitOfWork.Kurins.GetByKeyAsync(membership.KurinKey, cancellationToken);
            if (kurin is null || !kurin.ProfileVerificationEnabled)
            {
                continue;
            }

            recipientUserKeys.UnionWith(await _recipients.ResolveAsync(
                ReviewSubject.Profile,
                membership.KurinKey,
                membership.GroupKey,
                raised.ActorUserKey,
                cancellationToken));
        }

        if (recipientUserKeys.Count == 0)
        {
            return;
        }

        await _notifications.NotifyManyAsync(
            recipientUserKeys.Select(userKey => new NotificationRequest
            {
                RecipientUserKey = userKey,
                Type = AppNotificationType.MemberProfileChangedAfterVerification,
                Severity = AppNotificationSeverity.Warn,
                Title = "Профіль потребує повторної перевірки",
                Body = string.IsNullOrWhiteSpace(raised.MemberName)
                    ? "Дані верифікованого профілю змінилися. Перевір їх ще раз."
                    : $"Дані {raised.MemberName} змінилися після верифікації. Перевір їх ще раз.",
                EntityType = "Member",
                EntityKey = raised.MemberKey,
                Route = $"/member/{raised.MemberKey}",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"member-profile-stale:{raised.MemberKey}"
            }),
            cancellationToken);
    }
}
