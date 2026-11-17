namespace ProjectK.Common.Dtos.AgendaModule;

/// <summary>A just-issued calendar link: the only answer that ever carries it.</summary>
public sealed record CalendarFeedLinkDto(string FeedUrl, CalendarFeedStatusDto Status);
