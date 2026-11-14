namespace ProjectK.Common.Dtos.MeModule.Requests;

/// <summary>Body of <c>POST</c> and <c>PATCH api/me/calendar-feed</c>: what the link carries.</summary>
public sealed record CalendarFeedOptionsRequest(bool IncludeSchedules, bool IncludeTasks);
