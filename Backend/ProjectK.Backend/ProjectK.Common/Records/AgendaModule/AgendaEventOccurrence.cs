namespace ProjectK.Common.Records.AgendaModule;

/// <summary>One occurrence of an event, checked to be a day the series really falls on.</summary>
/// <param name="StartUtc">The occurrence's start, marked UTC.</param>
/// <param name="OccurrenceKey">What answers to this occurrence are kept under: its start for a series, null for a one-off.</param>
public sealed record AgendaEventOccurrence(AgendaEventSummary Event, DateTime StartUtc, DateTime? EndUtc, DateTime? OccurrenceKey);
