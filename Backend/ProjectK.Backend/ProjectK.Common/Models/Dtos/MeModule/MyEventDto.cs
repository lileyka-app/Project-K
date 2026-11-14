using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.MeModule;

/// <summary>One occurrence of an event ahead, from any of the person's kurins.</summary>
public sealed record MyEventDto
{
    public required Guid AgendaItemKey { get; init; }
    public required MyKurinRefDto Kurin { get; init; }
    public required string Title { get; init; }
    public required DateTime StartUtc { get; init; }
    public required DateTime? EndUtc { get; init; }
    public required bool IsAllDay { get; init; }
    public required bool IsRecurring { get; init; }
    public required string? Location { get; init; }
    public required string? CategoryName { get; init; }
    public required string? CategoryColorHex { get; init; }
    public required string? CategoryIcon { get; init; }
    public required bool RsvpRequired { get; init; }
    public required AgendaRsvpStatus? MyResponse { get; init; }
}
