using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Records.AgendaModule;

namespace ProjectK.Common.Contracts.AgendaModule;

/// <summary>
/// What the agenda module tells others about its tasks, events and groups of events. Every answer is a
/// record: a task's state for one person is worked out against the people its targets stand for now,
/// and a series' days in the kurin's zone, and only the agenda knows how to do either.
/// </summary>
public interface IAgendaDirectory
{
    /// <summary>
    /// The live tasks the viewer sees in their kurin, dated or not, each with the viewer's state and
    /// whether they may move it.
    /// </summary>
    Task<IReadOnlyList<AgendaViewerItem>> GetTasksForViewerAsync(
        AgendaViewerContext viewer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The dated items the viewer sees in their kurin within <paramref name="window"/>, events with their
    /// occurrences in it.
    /// </summary>
    Task<IReadOnlyList<AgendaViewerItem>> GetInWindowForViewerAsync(
        AgendaViewerContext viewer,
        AgendaWindow window,
        CancellationToken cancellationToken = default);

    /// <summary>The kurin's groups of events, by name, archived ones too when asked.</summary>
    Task<IReadOnlyList<AgendaCategorySummary>> GetCategoriesAsync(
        Guid kurinKey,
        bool includeArchived,
        CancellationToken cancellationToken = default);

    /// <summary>One group of events, of any kurin; null when there is none.</summary>
    Task<AgendaCategorySummary?> FindCategoryAsync(Guid agendaCategoryKey, CancellationToken cancellationToken = default);

    /// <summary>One event, of any kurin; null when there is none or it is a task.</summary>
    Task<AgendaEventSummary?> FindEventAsync(Guid agendaItemKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// One occurrence of an event of the kurin. Null when there is no such event here, it has no date,
    /// or the series has no occurrence starting at <paramref name="occurrenceStartUtc"/> in
    /// <paramref name="zone"/>.
    /// </summary>
    Task<AgendaEventOccurrence?> FindOccurrenceAsync(
        Guid kurinKey,
        Guid agendaItemKey,
        DateTime occurrenceStartUtc,
        TimeZoneInfo zone,
        CancellationToken cancellationToken = default);

    /// <summary>Everyone's answer to one occurrence of an event (null key for a one-off), by account.</summary>
    Task<IReadOnlyDictionary<Guid, AgendaRsvpStatus>> GetAnswersAsync(
        Guid agendaItemKey,
        DateTime? occurrenceKey,
        CancellationToken cancellationToken = default);

    /// <summary>The group of events each of these items belongs to, in one read; an item in none maps to null.</summary>
    Task<IReadOnlyDictionary<Guid, Guid?>> GetCategoryKeysAsync(
        IReadOnlyCollection<Guid> agendaItemKeys,
        CancellationToken cancellationToken = default);
}
