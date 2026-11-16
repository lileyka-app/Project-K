using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Records.AgendaModule;

/// <summary>One occurrence of an event, with the person's answer to it when that was asked for.</summary>
/// <param name="OccurrenceKey">What answers to this occurrence are kept under: its start for a series, null for a one-off.</param>
public sealed record AgendaViewerOccurrence(DateTime StartUtc, DateTime? EndUtc, DateTime? OccurrenceKey, AgendaRsvpStatus? Answer);
