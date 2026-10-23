using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Security;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.CalendarFeed.Issue;

/// <summary>
/// Issues the account's calendar link, replacing any earlier one: a calendar still subscribed to the
/// old link from then on shows only the notice that it is off.
/// </summary>
public sealed record IssueCalendarFeedCommand(bool IncludeSchedules, bool IncludeTasks) : IRequest<ServiceResult<IssuedCalendarFeedDto>>;

public sealed class IssueCalendarFeedCommandHandler : IRequestHandler<IssueCalendarFeedCommand, ServiceResult<IssuedCalendarFeedDto>>
{
    private readonly ICalendarFeedStore _feeds;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActivityLogger _activityLogger;

    public IssueCalendarFeedCommandHandler(ICalendarFeedStore feeds, ICurrentUserContext currentUser, IActivityLogger activityLogger)
    {
        _feeds = feeds;
        _currentUser = currentUser;
        _activityLogger = activityLogger;
    }

    public async Task<ServiceResult<IssuedCalendarFeedDto>> Handle(IssueCalendarFeedCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userKey)
        {
            return ServiceResult<IssuedCalendarFeedDto>.Failure(ResultType.Unauthorized, "NotSignedIn", "Not signed in.");
        }

        var token = SecretHash.NewToken();
        await _feeds.IssueAsync(userKey, SecretHash.Sha256Hex(token), request.IncludeSchedules, request.IncludeTasks, cancellationToken);

        _activityLogger.LogAudit(
            action: "Account.CalendarFeedIssued",
            actorUserId: userKey,
            targetUserId: userKey,
            reason: "Calendar subscription link issued; any earlier link revoked.");

        var feed = await _feeds.GetLatestForUserAsync(userKey, cancellationToken);
        return new ServiceResult<IssuedCalendarFeedDto>(ResultType.Success, new IssuedCalendarFeedDto
        {
            Token = token,
            Status = CalendarFeedStatusDto.Of(feed)
        });
    }
}
