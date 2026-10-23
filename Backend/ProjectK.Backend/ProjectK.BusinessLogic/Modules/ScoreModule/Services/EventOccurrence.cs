using System.Globalization;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>One occurrence of an event of the kurin, checked to be a day the series really falls on.</summary>
/// <param name="Zone">The kurin's zone, which the occurrence's day is named in.</param>
public sealed record EventOccurrence(AgendaItem Item, DateTime StartUtc, DateTime? EndUtc, TimeZoneInfo Zone)
{
    /// <summary>The event and the day it met on, in the kurin's zone: «Сходини · 06.10.2026».</summary>
    public string Label =>
        $"{Item.Title} · {TimeZoneInfo.ConvertTimeFromUtc(StartUtc, Zone).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Null when there is no such event here, it is a task, or the series has no occurrence starting
    /// at <paramref name="occurrenceStartUtc"/>: a mark on a day nobody met would be a mark on nothing.
    /// </summary>
    public static async Task<EventOccurrence?> LoadAsync(
        IUnitOfWork unitOfWork,
        IKraiProfileProvider profiles,
        Guid kurinKey,
        Guid agendaItemKey,
        DateTime occurrenceStartUtc,
        CancellationToken cancellationToken)
    {
        var item = await unitOfWork.AgendaItems.GetByKeyWithAssignmentsAsync(agendaItemKey, cancellationToken);
        if (item is null || item.KurinKey != kurinKey || item.Kind != AgendaItemKind.Event || !item.StartUtc.HasValue)
        {
            return null;
        }

        var wanted = DateTime.SpecifyKind(occurrenceStartUtc, DateTimeKind.Utc);
        var zone = await profiles.ForKurinAsync(kurinKey, cancellationToken);
        var occurrence = AgendaRecurrence.Expand(item, wanted.AddDays(-1), wanted.AddDays(1), zone)
            .FirstOrDefault(o => DateTime.SpecifyKind(o.StartUtc, DateTimeKind.Utc) == wanted);

        if (occurrence == default)
        {
            return null;
        }

        DateTime? endUtc = occurrence.EndUtc is { } end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : null;
        return new EventOccurrence(item, wanted, endUtc, zone);
    }
}
