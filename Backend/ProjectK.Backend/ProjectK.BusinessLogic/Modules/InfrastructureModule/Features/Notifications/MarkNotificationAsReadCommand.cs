using MediatR;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Features.Notifications;

public sealed record MarkNotificationAsReadCommand(Guid NotificationKey) : IRequest<ServiceResult<AppNotificationDto>>;

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
            return ServiceResult<AppNotificationDto>.Failure(ResultType.Unauthorized, InfrastructureErrors.Unauthorized, "No one is signed in.");
        }

        var notification = await _notificationService.MarkAsReadAsync(
            request.NotificationKey,
            cancellationToken);

        return notification is null
            ? ServiceResult<AppNotificationDto>.Failure(ResultType.NotFound, InfrastructureErrors.NotificationNotFound, "The notification was not found.")
            : new ServiceResult<AppNotificationDto>(ResultType.Success, notification);
    }
}
