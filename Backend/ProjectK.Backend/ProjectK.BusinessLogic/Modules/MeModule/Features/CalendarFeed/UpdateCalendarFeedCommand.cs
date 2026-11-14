using MediatR;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.MeModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.CalendarFeed;

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
            return MeErrors.SignInRequired<CalendarFeedStatusDto>();
        }

        if (!await _feeds.UpdateOptionsAsync(userKey, request.IncludeSchedules, request.IncludeTasks, cancellationToken))
        {
            return ServiceResult<CalendarFeedStatusDto>.Failure(ResultType.NotFound, MeErrors.CalendarFeedNotFound, "There is no live calendar link to change.");
        }

        var feed = await _feeds.GetLatestForUserAsync(userKey, cancellationToken);
        return new ServiceResult<CalendarFeedStatusDto>(ResultType.Success, CalendarFeedStatusDto.Of(feed));
    }
}
