using System.Globalization;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Records.AgendaModule;
using ProjectK.Common.Rules.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>One occurrence of an event of the kurin, checked to be a day the series really falls on.</summary>
/// <param name="OccurrenceKey">What answers to this occurrence are kept under: its start for a series, null for a one-off.</param>
/// <param name="Zone">The kurin's zone, which the occurrence's day is named in.</param>
public sealed record EventOccurrence(AgendaEventSummary Event, DateTime StartUtc, DateTime? EndUtc, DateTime? OccurrenceKey, TimeZoneInfo Zone)
{
    /// <summary>The event and the day it met on, in the kurin's zone: «Сходини · 06.10.2026».</summary>
    public string Label =>
        $"{Event.Title} · {TimeZoneInfo.ConvertTimeFromUtc(StartUtc, Zone).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Null when there is no such event here, it is a task, or the series has no occurrence starting
    /// at <paramref name="occurrenceStartUtc"/>: a mark on a day nobody met would be a mark on nothing.
    /// </summary>
    public static async Task<EventOccurrence?> LoadAsync(
        IAgendaDirectory agenda,
        IKraiProfileDirectory profiles,
        Guid kurinKey,
        Guid agendaItemKey,
        DateTime occurrenceStartUtc,
        CancellationToken cancellationToken)
    {
        var zone = await profiles.ForKurinAsync(kurinKey, cancellationToken);
        var found = await agenda.FindOccurrenceAsync(kurinKey, agendaItemKey, occurrenceStartUtc, zone, cancellationToken);
        return found is null ? null : new EventOccurrence(found.Event, found.StartUtc, found.EndUtc, found.OccurrenceKey, zone);
    }
}
