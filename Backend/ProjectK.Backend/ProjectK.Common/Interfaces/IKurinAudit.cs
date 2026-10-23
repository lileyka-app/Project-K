using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Interfaces;

/// <summary>
/// The kurin's journal of who did what. A handler records its action <b>before</b> its own
/// <c>SaveChangesAsync</c>: the entry rides on that save, so the change and its entry are committed
/// together or not at all. Recording after the save would need a second one, and a change that
/// succeeded could then go unrecorded.
/// </summary>
public interface IKurinAudit
{
    /// <summary>Adds an entry to the request's pending changes. Writes nothing on its own.</summary>
    Task RecordAsync(AuditRecord record, CancellationToken cancellationToken = default);
}
