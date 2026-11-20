using ProjectK.Common.Enums.AuditModule;

namespace ProjectK.Common.Dtos.AuditModule;

/// <summary>
/// One journal entry, complete: who did it and in which role, what it was about and to whom, and
/// what changed. Everything is a snapshot from the time, so the page needs no second lookup.
/// </summary>
public sealed record KurinJournalEntryDto(
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
    string? Note);
