using MediatR;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Features.Notifications;

public sealed record GetNotificationsQuery(bool UnreadOnly, int Take) : IRequest<ServiceResult<IReadOnlyList<AppNotificationDto>>>;

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
            return ServiceResult<IReadOnlyList<AppNotificationDto>>.Failure(ResultType.Unauthorized, InfrastructureErrors.Unauthorized, "No one is signed in.");
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
