using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.CalendarFeed.Disable;

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
            return ServiceResult<CalendarFeedStatusDto>.Failure(ResultType.Unauthorized, "NotSignedIn", "Not signed in.");
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
