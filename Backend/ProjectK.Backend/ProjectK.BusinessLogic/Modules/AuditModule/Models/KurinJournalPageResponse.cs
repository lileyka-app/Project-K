using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.AuditModule.Models;

/// <summary>One page of the kurin's journal, newest first.</summary>
/// <param name="Items">The entries.</param>
/// <param name="HasMore">Whether an older page follows.</param>
public sealed record KurinJournalPageResponse(IReadOnlyList<KurinJournalEntryResponse> Items, bool HasMore);

/// <summary>
/// One journal entry, complete: who did it and in which role, what it was about and to whom, and
/// what changed. Everything is a snapshot from the time, so the page needs no second lookup.
/// </summary>
public sealed record KurinJournalEntryResponse(
    Guid Key,
    DateTime OccurredAtUtc,
    AuditArea Area,
    string Action,
    Guid? ActorUserKey,
    string? ActorName,
    string ActorRole,
    Guid? SubjectMemberKey,
    string? SubjectMemberName,
    Guid SubjectKey,
    string SubjectLabel,
    IReadOnlyList<AuditChange> Changes,
    string? Note)
{
    public static KurinJournalEntryResponse FromEntity(KurinAuditEvent entry) => new(
        entry.KurinAuditEventKey,
        entry.OccurredAtUtc,
        entry.Area,
        entry.Action,
        entry.ActorUserKey,
        entry.ActorName,
        entry.ActorRole,
        entry.SubjectMemberKey,
        entry.SubjectMemberName,
        entry.SubjectKey,
        entry.SubjectLabel,
        entry.Changes,
        entry.Note);
}
