using MediatR;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Features.Notifications;

public sealed record MarkAllNotificationsAsReadCommand : IRequest<ServiceResult<int>>;

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
            return ServiceResult<int>.Failure(ResultType.Unauthorized, InfrastructureErrors.Unauthorized, "No one is signed in.");
        }

        var count = await _notificationService.MarkAllAsReadAsync(cancellationToken);
        return new ServiceResult<int>(ResultType.Success, count);
    }
}
