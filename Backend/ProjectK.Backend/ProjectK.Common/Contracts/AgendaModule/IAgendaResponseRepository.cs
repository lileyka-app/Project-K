using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AgendaModule;

namespace ProjectK.Common.Contracts.AgendaModule;

public interface IAgendaResponseRepository : IBaseEntityRepository<AgendaResponse>
{
    /// <summary>
    /// Every RSVP on one occurrence of an event (null = the one-off event itself), oldest first, so
    /// confirmed-vs-waitlist can be ranked by time.
    /// </summary>
    Task<IReadOnlyList<AgendaResponse>> GetForItemAsync(Guid agendaItemKey, DateTime? occurrenceStartUtc, CancellationToken cancellationToken = default);

    /// <summary>The current user's RSVP on one occurrence of an item, if any.</summary>
    Task<AgendaResponse?> GetForItemAndUserAsync(Guid agendaItemKey, Guid userKey, DateTime? occurrenceStartUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// One person's answers across many items in one query: the one-off answers plus the occurrence
    /// answers starting inside the window. What a date-range reader asks for instead of one lookup
    /// per occurrence.
    /// </summary>
    Task<IReadOnlyList<AgendaResponse>> GetForUserAsync(Guid userKey, IReadOnlyCollection<Guid> agendaItemKeys, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
