using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.MeModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.CalendarFeed;

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
            return MeErrors.SignInRequired<IssuedCalendarFeedDto>();
        }

        var token = SecretHashRules.NewToken();
        await _feeds.IssueAsync(userKey, SecretHashRules.Sha256Hex(token), request.IncludeSchedules, request.IncludeTasks, cancellationToken);

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
