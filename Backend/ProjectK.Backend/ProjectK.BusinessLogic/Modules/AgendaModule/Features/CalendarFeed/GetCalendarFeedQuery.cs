using MediatR;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Features.CalendarFeed;

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
            return AgendaErrors.SignInRequired<CalendarFeedStatusDto>();
        }

        var feed = await _feeds.GetLatestForUserAsync(userKey, cancellationToken);
        return new ServiceResult<CalendarFeedStatusDto>(ResultType.Success, CalendarFeedStatusDto.Of(feed));
    }
}
