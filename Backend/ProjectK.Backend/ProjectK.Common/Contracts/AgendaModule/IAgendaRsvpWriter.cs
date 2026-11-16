using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Contracts.AgendaModule;

/// <summary>
/// One answer per (event, occurrence, account): a fresh answer overwrites the old one for that
/// occurrence only. Shared by the calendar and the dashboard so both write the same row the same
/// way. The occurrence key comes resolved (<c>AgendaOccurrenceRules</c>). Does not save.
/// <para>
/// A write contract on purpose: the answer from the dashboard is the caller's own action, saved in
/// the caller's transaction and shown in its reply at once. An event handled after the save could
/// not give that.
/// </para>
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
