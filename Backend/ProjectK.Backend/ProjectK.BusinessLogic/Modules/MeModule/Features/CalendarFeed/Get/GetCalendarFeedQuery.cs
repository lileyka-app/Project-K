using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.CalendarFeed.Get;

/// <summary>Where the signed-in account's calendar link stands: none, live, or switched off and why.</summary>
public sealed record GetCalendarFeedQuery : IRequest<ServiceResult<CalendarFeedStatusDto>>;

public sealed class GetCalendarFeedQueryHandler : IRequestHandler<GetCalendarFeedQuery, ServiceResult<CalendarFeedStatusDto>>
{
    private readonly ICalendarFeedStore _feeds;
    private readonly ICurrentUserContext _currentUser;

    public GetCalendarFeedQueryHandler(ICalendarFeedStore feeds, ICurrentUserContext currentUser)
    {
        _feeds = feeds;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<CalendarFeedStatusDto>> Handle(GetCalendarFeedQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userKey)
        {
            return ServiceResult<CalendarFeedStatusDto>.Failure(ResultType.Unauthorized, "NotSignedIn", "Not signed in.");
        }

        var feed = await _feeds.GetLatestForUserAsync(userKey, cancellationToken);
        return new ServiceResult<CalendarFeedStatusDto>(ResultType.Success, CalendarFeedStatusDto.Of(feed));
    }
}
