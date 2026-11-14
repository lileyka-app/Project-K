using MediatR;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Models.Dtos.MeModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.CalendarFeed;

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
            return MeErrors.SignInRequired<CalendarFeedStatusDto>();
        }

        var feed = await _feeds.GetLatestForUserAsync(userKey, cancellationToken);
        return new ServiceResult<CalendarFeedStatusDto>(ResultType.Success, CalendarFeedStatusDto.Of(feed));
    }
}
