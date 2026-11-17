using MediatR;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Features.CalendarFeed;

/// <summary>Changes what the live link carries. The link stays the same, so nobody has to subscribe again.</summary>
public sealed record UpdateCalendarFeedCommand(bool IncludeSchedules, bool IncludeTasks) : IRequest<ServiceResult<CalendarFeedStatusDto>>;

public sealed class UpdateCalendarFeedCommandHandler : IRequestHandler<UpdateCalendarFeedCommand, ServiceResult<CalendarFeedStatusDto>>
{
    private readonly ICalendarFeedStore _feeds;
    private readonly ICurrentUserContext _currentUser;

    public UpdateCalendarFeedCommandHandler(ICalendarFeedStore feeds, ICurrentUserContext currentUser)
    {
        _feeds = feeds;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<CalendarFeedStatusDto>> Handle(UpdateCalendarFeedCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userKey)
        {
            return AgendaErrors.SignInRequired<CalendarFeedStatusDto>();
        }

        if (!await _feeds.UpdateOptionsAsync(userKey, request.IncludeSchedules, request.IncludeTasks, cancellationToken))
        {
            return ServiceResult<CalendarFeedStatusDto>.Failure(ResultType.NotFound, AgendaErrors.CalendarFeedNotFound, "There is no live calendar link to change.");
        }

        var feed = await _feeds.GetLatestForUserAsync(userKey, cancellationToken);
        return new ServiceResult<CalendarFeedStatusDto>(ResultType.Success, CalendarFeedMapping.ToDto(feed));
    }
}
