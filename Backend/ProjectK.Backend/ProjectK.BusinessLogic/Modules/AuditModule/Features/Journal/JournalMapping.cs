using ProjectK.Common.Dtos.AuditModule;
using ProjectK.Common.Entities.AuditModule;

namespace ProjectK.BusinessLogic.Modules.AuditModule.Features.Journal;

internal static class JournalMapping
{
    /// <summary>The entry as the journal page shows it, under the subject label the caller worked out.</summary>
    public static KurinJournalEntryDto ToDto(this KurinAuditEvent entry, string subjectLabel) => new(
        Key: entry.KurinAuditEventKey,
        OccurredAtUtc: entry.OccurredAtUtc,
        Area: entry.Area,
        Action: entry.Action,
        ActorUserKey: entry.ActorUserKey,
        ActorName: entry.ActorName,
        ActorRole: entry.ActorRole,
        SubjectMemberKey: entry.SubjectMemberKey,
        SubjectMemberName: entry.SubjectMemberName,
        SubjectKey: entry.SubjectKey,
        SubjectLabel: subjectLabel,
        Changes: entry.Changes,
        Note: entry.Note);
}
