using MediatR;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.MeModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.CalendarFeed;

/// <summary>Switches the link off at the person's own request. No notification: they did it themselves.</summary>
public sealed record DisableCalendarFeedCommand : IRequest<ServiceResult<CalendarFeedStatusDto>>;

public sealed class DisableCalendarFeedCommandHandler : IRequestHandler<DisableCalendarFeedCommand, ServiceResult<CalendarFeedStatusDto>>
{
    private readonly ICalendarFeedStore _feeds;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActivityLogger _activityLogger;

    public DisableCalendarFeedCommandHandler(ICalendarFeedStore feeds, ICurrentUserContext currentUser, IActivityLogger activityLogger)
    {
        _feeds = feeds;
        _currentUser = currentUser;
        _activityLogger = activityLogger;
    }

    public async Task<ServiceResult<CalendarFeedStatusDto>> Handle(DisableCalendarFeedCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userKey)
        {
            return MeErrors.SignInRequired<CalendarFeedStatusDto>();
        }

        if (await _feeds.RevokeActiveAsync(userKey, CalendarFeedRevokeReason.Disabled, cancellationToken))
        {
            _activityLogger.LogAudit(
                action: "Account.CalendarFeedDisabled",
                actorUserId: userKey,
                targetUserId: userKey,
                reason: "Calendar subscription link turned off by its owner.");
        }

        var feed = await _feeds.GetLatestForUserAsync(userKey, cancellationToken);
        return new ServiceResult<CalendarFeedStatusDto>(ResultType.Success, CalendarFeedStatusDto.Of(feed));
    }
}
