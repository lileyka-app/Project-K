using ProjectK.BusinessLogic.Modules.InfrastructureModule.Features.Notifications;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.InfrastructureModule;
using ProjectK.Common.Models.Dtos.InfrastructureModule;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Services;

public sealed class NotificationService : INotificationService
{
    private const int DefaultTake = 50;
    private const int MaxTake = 100;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPushQueue _pushQueue;

    public NotificationService(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IPushQueue pushQueue)
    {
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _pushQueue = pushQueue;
    }

    public async Task<AppNotificationDto?> NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.RecipientUserKey == Guid.Empty)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var deduplicationKey = Normalize(request.DeduplicationKey, 300);

        AppNotification? notification = null;
        if (!string.IsNullOrWhiteSpace(deduplicationKey))
        {
            notification = await _unitOfWork.AppNotifications.GetUnreadByDeduplicationKeyAsync(
                request.RecipientUserKey,
                deduplicationKey,
                now,
                cancellationToken);
        }

        var isNew = notification is null;
        if (notification is null)
        {
            notification = new AppNotification
            {
                RecipientUserKey = request.RecipientUserKey,
                CreatedAtUtc = now,
                CreatedDate = now
            };

            _unitOfWork.AppNotifications.Create(notification, cancellationToken);
        }

        ApplyRequest(notification, request, now, deduplicationKey);

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return null;
        }

        var saved = notification.ToDto();
        // Only a new entry reaches the phone: a repeat folded into an unread one must not buzz again.
        if (isNew)
        {
            _pushQueue.Enqueue(saved);
        }

        return saved;
    }

    public async Task<IReadOnlyList<AppNotificationDto>> NotifyManyAsync(
        IEnumerable<NotificationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var responses = new List<AppNotificationDto>();
        foreach (var request in requests)
        {
            var response = await NotifyAsync(request, cancellationToken);
            if (response is not null)
            {
                responses.Add(response);
            }
        }

        return responses;
    }

    public async Task<IReadOnlyList<AppNotificationDto>> GetInboxAsync(
        NotificationQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return [];
        }

        var take = NormalizeTake(query.Take);
        var notifications = await _unitOfWork.AppNotifications.GetInboxAsync(
            _currentUserContext.UserId.Value,
            query.UnreadOnly,
            take,
            DateTime.UtcNow,
            cancellationToken);

        return notifications.Select(notification => notification.ToDto()).ToList();
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return 0;
        }

        return await _unitOfWork.AppNotifications.GetUnreadCountAsync(
            _currentUserContext.UserId.Value,
            DateTime.UtcNow,
            cancellationToken);
    }

    public async Task<AppNotificationDto?> MarkAsReadAsync(Guid notificationKey, CancellationToken cancellationToken = default)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return null;
        }

        var notification = await _unitOfWork.AppNotifications.GetByRecipientAndKeyAsync(
            _currentUserContext.UserId.Value,
            notificationKey,
            cancellationToken);

        if (notification is null)
        {
            return null;
        }

        if (!notification.ReadAtUtc.HasValue)
        {
            var now = DateTime.UtcNow;
            notification.ReadAtUtc = now;
            notification.UpdatedDate = now;
            _unitOfWork.AppNotifications.Update(notification, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return notification.ToDto();
    }

    public async Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return 0;
        }

        return await _unitOfWork.AppNotifications.MarkAllAsReadAsync(
            _currentUserContext.UserId.Value,
            DateTime.UtcNow,
            cancellationToken);
    }

    private static void ApplyRequest(
        AppNotification notification,
        NotificationRequest request,
        DateTime now,
        string? deduplicationKey)
    {
        notification.Type = request.Type;
        notification.Severity = request.Severity;
        notification.Title = NormalizeRequired(request.Title, 200);
        notification.Body = NormalizeRequired(request.Body, 1000);
        notification.EntityType = Normalize(request.EntityType, 100);
        notification.EntityKey = request.EntityKey;
        notification.Route = Normalize(request.Route, 1000);
        notification.PayloadJson = Normalize(request.PayloadJson, 2000);
        notification.ActorUserKey = request.ActorUserKey;
        notification.DeduplicationKey = deduplicationKey;
        notification.ExpiresAtUtc = request.ExpiresAtUtc;
        notification.CreatedAtUtc = now;
        notification.UpdatedDate = now;
    }

    private static int NormalizeTake(int take)
    {
        if (take <= 0)
        {
            return DefaultTake;
        }

        return Math.Min(take, MaxTake);
    }

    private static string NormalizeRequired(string value, int maxLength)
    {
        var normalized = Normalize(value, maxLength);
        return string.IsNullOrWhiteSpace(normalized) ? "-" : normalized;
    }

    private static string? Normalize(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength];
    }
}
