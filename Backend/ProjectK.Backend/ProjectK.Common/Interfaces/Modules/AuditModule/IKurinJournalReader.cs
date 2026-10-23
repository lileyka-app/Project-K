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
}
