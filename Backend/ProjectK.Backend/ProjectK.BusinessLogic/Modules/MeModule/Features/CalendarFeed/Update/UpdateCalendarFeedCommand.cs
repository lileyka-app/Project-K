using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.CalendarFeed.Update;

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
            return ServiceResult<CalendarFeedStatusDto>.Failure(ResultType.Unauthorized, "NotSignedIn", "Not signed in.");
        }

        if (!await _feeds.UpdateOptionsAsync(userKey, request.IncludeSchedules, request.IncludeTasks, cancellationToken))
        {
            return ServiceResult<CalendarFeedStatusDto>.Failure(ResultType.NotFound, "CalendarFeedNotFound", "There is no live calendar link to change.");
        }

        var feed = await _feeds.GetLatestForUserAsync(userKey, cancellationToken);
        return new ServiceResult<CalendarFeedStatusDto>(ResultType.Success, CalendarFeedStatusDto.Of(feed));
    }
}
