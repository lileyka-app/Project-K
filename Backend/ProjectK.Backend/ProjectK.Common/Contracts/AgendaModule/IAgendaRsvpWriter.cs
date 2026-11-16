using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Records.AgendaModule;

namespace ProjectK.Common.Contracts.AgendaModule;

/// <summary>
/// One answer per (event, occurrence, account): a fresh answer overwrites the old one for that
/// occurrence only. Shared by the calendar and the dashboard so both write the same row the same
/// way. Does not save.
/// <para>
/// A write contract on purpose: the answer from the dashboard is the caller's own action, saved in
/// the caller's transaction and shown in its reply at once. An event handled after the save could
/// not give that.
/// </para>
/// </summary>
public interface IAgendaRsvpWriter
{
    /// <summary>
    /// The person's answer to one occurrence of an event of any kurin they stand in. Checks what the
    /// calendar checks: that it is an event, that the person sees it from its kurin
    /// (<paramref name="viewers"/> holds how each of their kurins sees them) and that the series has an
    /// occurrence starting at <paramref name="occurrenceStartUtc"/> (null for a one-off).
    /// </summary>
    Task<AgendaRsvpOutcome> AnswerAsync(
        IReadOnlyCollection<AgendaViewerContext> viewers,
        Guid agendaItemKey,
        DateTime? occurrenceStartUtc,
        AgendaRsvpStatus status,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    /// <summary>Stages the answer under an occurrence key already resolved (<c>AgendaOccurrenceRules</c>).</summary>
    Task UpsertAsync(
        Guid agendaItemKey,
        Guid userKey,
        DateTime? occurrenceKey,
        AgendaRsvpStatus status,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
