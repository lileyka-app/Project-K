using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Interfaces.Modules.AuditModule;

/// <summary>Reads a kurin's journal. Filtering and paging happen in the database.</summary>
public interface IKurinJournalReader
{
    /// <summary>The entries <paramref name="filter"/> selects, newest first, one page of them.</summary>
    Task<IReadOnlyList<KurinAuditEvent>> ReadAsync(
        KurinJournalFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every entry about any of these records, oldest first, whatever kurin it was filed under. One
    /// query for a whole list, so a screen that shows the history of many records asks once.
    /// </summary>
    Task<IReadOnlyList<KurinAuditEvent>> GetForSubjectsAsync(
        IReadOnlyCollection<Guid> subjectKeys,
        CancellationToken cancellationToken = default);
}
