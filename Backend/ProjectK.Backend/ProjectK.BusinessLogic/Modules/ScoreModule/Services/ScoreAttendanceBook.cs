using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>
/// The attendance part of a kurin's точкування alone: what a mark at an event is worth, which
/// occurrences were marked, and which did not take place. For reads that never touch the ledger —
/// three queries instead of the whole <see cref="ScoreBook"/>.
/// </summary>
public sealed class ScoreAttendanceBook
{
    public ScoreAttendanceBook(
        IReadOnlyList<ScoreAttendanceRate> rates,
        IReadOnlyList<ScoreAttendance> attendances,
        IReadOnlyList<ScoreNotHeldOccurrence> notHeld)
    {
        Rates = rates;
        Attendances = attendances;
        NotHeld = notHeld;
    }

    public IReadOnlyList<ScoreAttendanceRate> Rates { get; }
    public IReadOnlyList<ScoreAttendance> Attendances { get; }
    public IReadOnlyList<ScoreNotHeldOccurrence> NotHeld { get; }

    public int AttendancePoints(Guid agendaItemKey, Guid? categoryKey) => PointsFor(Rates, agendaItemKey, categoryKey);

    /// <summary>What a mark at this event is worth now: its own rate, else its group's, else nothing.</summary>
    public static int PointsFor(IReadOnlyList<ScoreAttendanceRate> rates, Guid agendaItemKey, Guid? categoryKey)
    {
        var own = rates.FirstOrDefault(r => r.AgendaItemKey == agendaItemKey);
        if (own is not null)
        {
            return own.Points;
        }

        return categoryKey is { } category
            ? rates.FirstOrDefault(r => r.AgendaCategoryKey == category)?.Points ?? 0
            : 0;
    }
}
