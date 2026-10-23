using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Notifications;

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
    private readonly IReviewNotificationRecipientResolver _recipients;
    private readonly IUnitOfWork _unitOfWork;

    public MemberProfileWentStaleEventHandler(
        INotificationService notifications,
        IReviewNotificationRecipientResolver recipients,
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

public sealed class MemberProfileVerifiedEventHandler
    : INotificationHandler<DomainEventNotification<MemberProfileVerified>>
{
    private readonly INotificationService _notifications;

    public MemberProfileVerifiedEventHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public Task Handle(DomainEventNotification<MemberProfileVerified> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        return _notifications.NotifyAsync(
            new NotificationRequest
            {
                RecipientUserKey = raised.MemberUserKey,
                Type = AppNotificationType.MemberProfileVerified,
                Severity = AppNotificationSeverity.Success,
                Title = "Профільні дані підтверджено",
                Body = "Ваші профільні дані підтверджено як актуальні.",
                EntityType = "Member",
                EntityKey = raised.MemberKey,
                Route = $"/member/{raised.MemberKey}",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"member-profile-verified:{raised.MemberKey}"
            },
            cancellationToken);
    }
}

public sealed class MemberAwardSubmittedEventHandler
    : INotificationHandler<DomainEventNotification<MemberAwardSubmitted>>
{
    private readonly INotificationService _notifications;
    private readonly IReviewNotificationRecipientResolver _recipients;

    public MemberAwardSubmittedEventHandler(
        INotificationService notifications,
        IReviewNotificationRecipientResolver recipients)
    {
        _notifications = notifications;
        _recipients = recipients;
    }

    public async Task Handle(DomainEventNotification<MemberAwardSubmitted> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        var recipientUserKeys = await _recipients.ResolveAsync(
            ReviewSubject.Award,
            raised.KurinKey,
            raised.GroupKey,
            raised.ActorUserKey,
            cancellationToken);

        if (recipientUserKeys.Count == 0)
        {
            return;
        }

        await _notifications.NotifyManyAsync(
            recipientUserKeys.Select(userKey => new NotificationRequest
            {
                RecipientUserKey = userKey,
                Type = AppNotificationType.MemberAwardSubmitted,
                Severity = AppNotificationSeverity.Info,
                Title = "Відзначення подано на розгляд",
                Body = string.IsNullOrWhiteSpace(raised.MemberName)
                    ? "Надійшло відзначення на розгляд."
                    : $"Надійшло відзначення від {raised.MemberName} на розгляд.",
                EntityType = "MemberAward",
                EntityKey = raised.MemberAwardKey,
                Route = $"/member/{raised.MemberKey}",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"award-submitted:{raised.MemberAwardKey}"
            }),
            cancellationToken);
    }
}

public sealed class MemberAwardReviewedEventHandler
    : INotificationHandler<DomainEventNotification<MemberAwardReviewed>>
{
    private readonly INotificationService _notifications;

    public MemberAwardReviewedEventHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public Task Handle(DomainEventNotification<MemberAwardReviewed> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        return _notifications.NotifyAsync(
            new NotificationRequest
            {
                RecipientUserKey = raised.MemberUserKey,
                Type = AppNotificationType.MemberAwardReviewed,
                Severity = raised.IsApproved ? AppNotificationSeverity.Success : AppNotificationSeverity.Warn,
                Title = raised.IsApproved ? "Відзначення затверджено" : "Відзначення не затверджено",
                Body = raised.IsApproved
                    ? "Ваше відзначення затверджено."
                    : "Ваше відзначення не затверджено. Перегляньте зауваження.",
                EntityType = "MemberAward",
                EntityKey = raised.MemberAwardKey,
                Route = $"/member/{raised.MemberKey}",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"award-review:{raised.MemberAwardKey}"
            },
            cancellationToken);
    }
}

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

public sealed class BadgeProgressSubmittedEventHandler
    : INotificationHandler<DomainEventNotification<BadgeProgressSubmitted>>
{
    private readonly INotificationService _notifications;
    private readonly IReviewNotificationRecipientResolver _recipients;

    public BadgeProgressSubmittedEventHandler(
        INotificationService notifications,
        IReviewNotificationRecipientResolver recipients)
    {
        _notifications = notifications;
        _recipients = recipients;
    }

    public async Task Handle(DomainEventNotification<BadgeProgressSubmitted> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        var recipientUserKeys = await _recipients.ResolveAsync(
            ReviewSubject.Badge,
            raised.KurinKey,
            raised.GroupKey,
            raised.ActorUserKey,
            cancellationToken);

        if (recipientUserKeys.Count == 0)
        {
            return;
        }

        await _notifications.NotifyManyAsync(
            recipientUserKeys.Select(userKey => new NotificationRequest
            {
                RecipientUserKey = userKey,
                Type = AppNotificationType.MemberSkillSubmittedForReview,
                Severity = AppNotificationSeverity.Info,
                Title = "Вмілість подано на перевірку",
                Body = string.IsNullOrWhiteSpace(raised.MemberName)
                    ? "Надійшла вмілість на перевірку."
                    : $"Надійшла вмілість від {raised.MemberName} на перевірку.",
                EntityType = "BadgeProgress",
                EntityKey = raised.MemberKey,
                Route = $"/kurin/{raised.KurinKey}/review/skills",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"skill-review:{raised.MemberKey}:{raised.BadgeId}"
            }),
            cancellationToken);
    }
}

public sealed class BadgeProgressReviewedEventHandler
    : INotificationHandler<DomainEventNotification<BadgeProgressReviewed>>
{
    private readonly INotificationService _notifications;

    public BadgeProgressReviewedEventHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public Task Handle(DomainEventNotification<BadgeProgressReviewed> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;

        var (title, body) = raised switch
        {
            { IsApproved: true } => ("Вмілість зараховано",
                "Вашу вмілість зараховано."),
            { ConfirmationWithdrawn: true } => ("Підтвердження вмілості скасовано",
                "Раніше зараховану вмілість вилучено."),
            _ => ("Вмілість потребує доопрацювання",
                "Вашу вмілість не зараховано. Перегляньте зауваження та подайте її повторно.")
        };

        return _notifications.NotifyAsync(
            new NotificationRequest
            {
                RecipientUserKey = raised.MemberUserKey,
                Type = AppNotificationType.MemberSkillReviewed,
                Severity = raised.IsApproved ? AppNotificationSeverity.Success : AppNotificationSeverity.Warn,
                Title = title,
                Body = body,
                EntityType = "BadgeProgress",
                EntityKey = raised.BadgeProgressKey,
                Route = $"/member/{raised.MemberKey}",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"skill-review-result:{raised.MemberKey}:{raised.BadgeId}"
            },
            cancellationToken);
    }
}
