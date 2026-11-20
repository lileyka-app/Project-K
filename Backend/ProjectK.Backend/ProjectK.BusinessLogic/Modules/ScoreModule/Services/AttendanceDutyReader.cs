using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Records.AgendaModule;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Rules.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>
/// «Справи» from точкування: events of the last two weeks worth points where no one at all was marked
/// present — the sheet nobody opened — unless someone said that occurrence did not take place. Only
/// for someone who scores at least one гурток there, and never in a kurin that switched точкування off.
/// </summary>
public sealed class AttendanceDutyReader : IDutySource
{
    private const int AttendanceWindowDays = 14;

    private readonly ScoreBookReader _books;
    private readonly IAgendaDirectory _agenda;
    private readonly IKraiProfileDirectory _profiles;
    private readonly IKurinDirectory _kurins;
    private readonly TimeProvider _time;

    public AttendanceDutyReader(ScoreBookReader books, IAgendaDirectory agenda, IKraiProfileDirectory profiles, IKurinDirectory kurins, TimeProvider time)
    {
        _books = books;
        _agenda = agenda;
        _profiles = profiles;
        _kurins = kurins;
        _time = time;
    }

    public async Task<IReadOnlyList<PendingDuty>> GetDutiesAsync(DutyViewer viewer, CancellationToken cancellationToken = default)
    {
        if (!viewer.Rights.ForAnyGroup(ResourceType.GroupScore, ResourceAction.Create)
            || !await _kurins.IsScoreEnabledAsync(viewer.KurinKey, cancellationToken))
        {
            return [];
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var from = now.AddDays(-AttendanceWindowDays);
        var book = await _books.OpenAttendanceAsync(viewer.KurinKey, cancellationToken);
        var zone = await _profiles.ForKurinAsync(viewer.KurinKey, cancellationToken);
        var settled = book.Attendances
            .Where(a => a.RemovedAtUtc is null)
            .Select(a => (a.AgendaItemKey, a.OccurrenceStartUtc))
            .ToHashSet();
        // An occurrence said not to have taken place has no sheet to fill.
        settled.UnionWith(book.NotHeld.Select(n => (n.AgendaItemKey, n.OccurrenceStartUtc)));

        var items = await _agenda.GetInWindowForViewerAsync(
            viewer.Agenda, new AgendaWindow(from, now, zone, AgendaItemKind.Event), cancellationToken);

        return items
            .Where(candidate => book.AttendancePoints(candidate.AgendaItemKey, candidate.AgendaCategoryKey) > 0)
            .SelectMany(item => item.Occurrences
                .Select(occurrence => occurrence.StartUtc)
                .Where(startUtc => startUtc <= now && !settled.Contains((item.AgendaItemKey, startUtc)))
                .Select(startUtc => new PendingDuty(
                    MyDutyKind.EventWithoutAttendance,
                    1,
                    AgendaItemKey: item.AgendaItemKey,
                    OccurrenceStartUtc: DateTime.SpecifyKind(startUtc, DateTimeKind.Utc),
                    Title: item.Title)))
            .ToList();
    }
}
