using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Dtos.MeModule.Requests;

/// <summary>Body for a dashboard RSVP; <c>OccurrenceStartUtc</c> names the occurrence of a series, null for a one-off event.</summary>
public sealed record SetMyEventResponseRequest(AgendaRsvpStatus? Status, DateTime? OccurrenceStartUtc = null);
