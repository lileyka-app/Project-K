namespace ProjectK.BusinessLogic.Modules.AgendaModule.Models;

/// <summary>
/// One event of the calendar link. Occurrences of a series arrive already expanded, so each one is
/// an event of its own and the file needs neither RRULE nor VTIMEZONE.
/// </summary>
/// <param name="StartUtc">For an all-day event, midnight UTC of its first day.</param>
/// <param name="EndUtc">For an all-day event, its last day (inclusive, as stored); for a timed one, the end instant.</param>
public sealed record IcsEvent(
    string Uid,
    DateTime StartUtc,
    DateTime EndUtc,
    bool IsAllDay,
    string Summary,
    string? Description,
    string? Location,
    string? Category,
    DateTime StampUtc);
