using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Interfaces.Modules.KurinModule;

public interface IAgendaItemRepository : IBaseEntityRepository<AgendaItem>
{
    /// <summary>Loads an item with its assignments and their per-person parts for detail, edit and authorization checks.</summary>
    Task<AgendaItem?> GetByKeyWithAssignmentsAsync(Guid agendaItemKey, CancellationToken token = default);

    /// <summary>The group of events each item belongs to, in one query; items that no longer exist are absent.</summary>
    Task<IReadOnlyDictionary<Guid, Guid?>> GetCategoryKeysAsync(IReadOnlyCollection<Guid> agendaItemKeys, CancellationToken token = default);

    /// <summary>Marks an assignment for insertion. Explicit Added state avoids collection-fixup ambiguity.</summary>
    void AddAssignment(AgendaAssignment assignment);

    /// <summary>Marks a person's part of a target for insertion, for the same reason as <see cref="AddAssignment"/>.</summary>
    void AddProgress(AgendaAssignmentProgress progress);

    /// <summary>Marks an assignment for deletion. Explicit Deleted state avoids an accidental UPDATE.</summary>
    void RemoveAssignment(AgendaAssignment assignment);

    /// <summary>Nulls the category on every item that referenced it — run before deleting an event group.</summary>
    Task ClearCategoryAsync(Guid agendaCategoryKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops every assignment pointing at one of <paramref name="targetKeys"/> — run before deleting
    /// the гурток, member or провід they name.
    /// <para>
    /// <c>AgendaAssignment.TargetKey</c> is polymorphic across five tables, so no foreign key can
    /// clean up after a delete. Left behind, the row names something that no longer exists: it
    /// reaches nobody, yet still counts as the item's assignment, so an item assigned only to a
    /// deleted гурток quietly stops appearing for everyone below whole-kurin scope.
    /// </para>
    /// </summary>
    Task RemoveAssignmentsForTargetsAsync(IEnumerable<Guid> targetKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Items visible to <paramref name="viewer"/>. <paramref name="onlyDated"/> keeps the calendar to
    /// placed items; <paramref name="fromUtc"/>/<paramref name="toUtc"/> narrow to a window;
    /// <paramref name="kind"/> narrows the board to tasks; <paramref name="archived"/> reads the
    /// archive instead of what is live; <paramref name="includeKurinSchedules"/> adds every event of a
    /// group marked «графік куреня». Assignments are included.
    /// </summary>
    Task<IEnumerable<AgendaItem>> GetForViewerAsync(
        AgendaViewerScope viewer,
        DateTime? fromUtc,
        DateTime? toUtc,
        bool onlyDated,
        AgendaItemKind? kind,
        CancellationToken token = default,
        bool archived = false,
        bool includeKurinSchedules = false);
}
