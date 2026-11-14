using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Contracts.AgendaModule;

/// <summary>
/// One answer per (event, occurrence, account): a fresh answer overwrites the old one for that
/// occurrence only. Shared by the calendar and the dashboard so both write the same row the same
/// way. The occurrence key comes resolved (<c>AgendaOccurrenceRules</c>). Does not save.
/// </summary>
public interface IAgendaRsvpWriter
{
    Task UpsertAsync(
        AgendaItem item,
        Guid userKey,
        DateTime? occurrenceKey,
        AgendaRsvpStatus status,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
