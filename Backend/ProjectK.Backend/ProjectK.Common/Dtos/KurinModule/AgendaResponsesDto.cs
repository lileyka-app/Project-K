using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>The full RSVP picture for one occurrence of an event: everyone's answer, the counts, and the caller's own choice.</summary>
public sealed record AgendaResponsesDto
{
    public required Guid AgendaItemKey { get; init; }

    /// <summary>The occurrence the picture is of; null for a one-off event.</summary>
    public required DateTime? OccurrenceStartUtc { get; init; }
    public required int? Capacity { get; init; }
    public required bool WaitlistEnabled { get; init; }
    public required AgendaRsvpStatus? MyStatus { get; init; }
    public required int GoingConfirmedCount { get; init; }
    public required int GoingWaitlistCount { get; init; }
    public required int NotGoingCount { get; init; }
    public required int MaybeCount { get; init; }
    public required List<AgendaRsvpDto> Responses { get; init; }
}
