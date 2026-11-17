using MediatR;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.NotificationsModule.Features.Notifications;

public sealed record GetUnreadNotificationCountQuery : IRequest<ServiceResult<int>>;

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
            return ServiceResult<int>.Failure(ResultType.Unauthorized, NotificationsErrors.Unauthorized, "No one is signed in.");
        }

        var count = await _notificationService.GetUnreadCountAsync(cancellationToken);
        return new ServiceResult<int>(ResultType.Success, count);
    }
}
