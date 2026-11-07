using MediatR;
using Microsoft.Extensions.Logging;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Notifications;

/// <summary>
/// The danger zone of the archive reaches the other Звʼязкові and the курінний: records are about
/// to go for good, and they are the ones who would want to know who did it.
/// </summary>
public sealed class ArchiveDangerZoneEnteredEventHandler : INotificationHandler<DomainEventNotification<ArchiveDangerZoneEntered>>
{
    private readonly INotificationService _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ArchiveDangerZoneEnteredEventHandler> _logger;

    public ArchiveDangerZoneEnteredEventHandler(
        INotificationService notifications,
        IUnitOfWork unitOfWork,
        ILogger<ArchiveDangerZoneEnteredEventHandler> logger)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<ArchiveDangerZoneEntered> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        try
        {
            await NotifyAsync(raised, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Danger zone opened in kurin {KurinKey}, but its leadership could not be told", raised.KurinKey);
        }
    }

    private async Task NotifyAsync(ArchiveDangerZoneEntered raised, CancellationToken cancellationToken)
    {
        var zvyazkovyi = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi);
        var kurinnyi = SystemRole.ForOffice(LeadershipType.Kurin, LeadershipRole.Kurinnuy);
        var holders = (await _unitOfWork.Leaderships.GetOfficeMembersLookupAsync(raised.KurinKey, LeadershipType.KV, cancellationToken))
            .Where(holder => holder.UserRole == zvyazkovyi)
            .Concat((await _unitOfWork.Leaderships.GetOfficeMembersLookupAsync(raised.KurinKey, LeadershipType.Kurin, cancellationToken))
                .Where(holder => holder.UserRole == kurinnyi));
        // Only who stands in the kurin now: an office row of someone who left is history, not a seat.
        var standing = (await _unitOfWork.Memberships.GetAccountKeysInKurinAsync(raised.KurinKey, cancellationToken)).ToHashSet();
        var recipients = holders
            .Select(holder => holder.UserKey)
            .OfType<Guid>()
            .Where(userKey => userKey != raised.ActorUserKey && standing.Contains(userKey))
            .Distinct()
            .ToList();
        if (recipients.Count == 0)
        {
            return;
        }

        var at = DateTime.UtcNow;
        await _notifications.NotifyManyAsync(
            recipients.Select(userKey => new NotificationRequest
            {
                RecipientUserKey = userKey,
                Type = AppNotificationType.ArchiveDangerZoneEntered,
                Severity = AppNotificationSeverity.Warn,
                Title = "Небезпечний режим архіву",
                Body = string.IsNullOrWhiteSpace(raised.ActorName) ? "Увімкнено небезпечний режим." : $"Увімкнено небезпечний режим: {raised.ActorName}.",
                EntityType = "KurinArchive",
                EntityKey = raised.KurinKey,
                Route = $"/kurin/{raised.KurinKey}/journal",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"archive-danger:{raised.KurinKey}:{raised.ActorUserKey}:{at:yyyyMMddHHmm}"
            }),
            cancellationToken);
    }
}
