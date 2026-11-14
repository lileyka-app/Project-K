using MediatR;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Notifications;

public sealed record GetNotificationsQuery(bool UnreadOnly, int Take) : IRequest<ServiceResult<IReadOnlyList<AppNotificationDto>>>;

public sealed record GetUnreadNotificationCountQuery : IRequest<ServiceResult<int>>;

public sealed record MarkNotificationAsReadCommand(Guid NotificationKey) : IRequest<ServiceResult<AppNotificationDto>>;

public sealed record MarkAllNotificationsAsReadCommand : IRequest<ServiceResult<int>>;

public sealed class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, ServiceResult<IReadOnlyList<AppNotificationDto>>>
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserContext _currentUserContext;

    public GetNotificationsQueryHandler(
        INotificationService notificationService,
        ICurrentUserContext currentUserContext)
    {
        _notificationService = notificationService;
        _currentUserContext = currentUserContext;
    }

    public async Task<ServiceResult<IReadOnlyList<AppNotificationDto>>> Handle(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return new ServiceResult<IReadOnlyList<AppNotificationDto>>(ResultType.Unauthorized);
        }

        var inbox = await _notificationService.GetInboxAsync(
            new NotificationQuery
            {
                UnreadOnly = request.UnreadOnly,
                Take = request.Take
            },
            cancellationToken);

        return new ServiceResult<IReadOnlyList<AppNotificationDto>>(ResultType.Success, inbox);
    }
}

public sealed class GetUnreadNotificationCountQueryHandler
    : IRequestHandler<GetUnreadNotificationCountQuery, ServiceResult<int>>
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserContext _currentUserContext;

    public GetUnreadNotificationCountQueryHandler(
        INotificationService notificationService,
        ICurrentUserContext currentUserContext)
    {
        _notificationService = notificationService;
        _currentUserContext = currentUserContext;
    }

    public async Task<ServiceResult<int>> Handle(
        GetUnreadNotificationCountQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return new ServiceResult<int>(ResultType.Unauthorized);
        }

        var count = await _notificationService.GetUnreadCountAsync(cancellationToken);
        return new ServiceResult<int>(ResultType.Success, count);
    }
}

public sealed class MarkNotificationAsReadCommandHandler
    : IRequestHandler<MarkNotificationAsReadCommand, ServiceResult<AppNotificationDto>>
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserContext _currentUserContext;

    public MarkNotificationAsReadCommandHandler(
        INotificationService notificationService,
        ICurrentUserContext currentUserContext)
    {
        _notificationService = notificationService;
        _currentUserContext = currentUserContext;
    }

    public async Task<ServiceResult<AppNotificationDto>> Handle(
        MarkNotificationAsReadCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return new ServiceResult<AppNotificationDto>(ResultType.Unauthorized);
        }

        var notification = await _notificationService.MarkAsReadAsync(
            request.NotificationKey,
            cancellationToken);

        return notification is null
            ? new ServiceResult<AppNotificationDto>(ResultType.NotFound)
            : new ServiceResult<AppNotificationDto>(ResultType.Success, notification);
    }
}

public sealed class MarkAllNotificationsAsReadCommandHandler
    : IRequestHandler<MarkAllNotificationsAsReadCommand, ServiceResult<int>>
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserContext _currentUserContext;

    public MarkAllNotificationsAsReadCommandHandler(
        INotificationService notificationService,
        ICurrentUserContext currentUserContext)
    {
        _notificationService = notificationService;
        _currentUserContext = currentUserContext;
    }

    public async Task<ServiceResult<int>> Handle(
        MarkAllNotificationsAsReadCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return new ServiceResult<int>(ResultType.Unauthorized);
        }

        var count = await _notificationService.MarkAllAsReadAsync(cancellationToken);
        return new ServiceResult<int>(ResultType.Success, count);
    }
}
