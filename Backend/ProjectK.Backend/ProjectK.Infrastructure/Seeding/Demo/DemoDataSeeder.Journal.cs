using ProjectK.Common.Dtos.AuditModule;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.AuthModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.Infrastructure.Seeding.Demo;

/// <summary>The journal entries the handlers would have written for what the other passes seed.</summary>
public partial class DemoDataSeeder
{
    private static readonly string ZvyazkovyiRole = SystemRoleNames.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi);
    private static readonly string VykhovnykRole = SystemRoleNames.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk);

    /// <summary>
    /// The journal entry the status handlers write for one step of a проба or a вмілість, so the
    /// demo's history reads as if the people in it had done it through the app.
    /// </summary>
    private void AddJournalEntry(
        Person subject,
        Guid kurinKey,
        AuditArea area,
        string action,
        Guid subjectKey,
        string label,
        Enum? fromStatus,
        Enum toStatus,
        Person actor,
        string actorRole,
        DateTime occurredAtUtc,
        string? note) =>
        AddJournalEntry(
            subject, kurinKey, area, action, subjectKey, label,
            AuditChangeRules.Of(("status", fromStatus, toStatus)), actor, actorRole, occurredAtUtc, note);

    /// <summary>The journal entry a handler writes for one change of a member's record.</summary>
    private void AddJournalEntry(
        Person subject,
        Guid kurinKey,
        AuditArea area,
        string action,
        Guid subjectKey,
        string label,
        IReadOnlyList<AuditChange> changes,
        Person actor,
        string actorRole,
        DateTime occurredAtUtc,
        string? note = null)
    {
        _dbContext.KurinAuditEvents.Add(new KurinAuditEvent
        {
            KurinKey = kurinKey,
            OccurredAtUtc = occurredAtUtc,
            ActorUserKey = actor.UserKey,
            ActorName = actor.FullName,
            ActorRole = actorRole,
            Area = area,
            Action = action,
            SubjectMemberKey = subject.Key,
            SubjectMemberName = subject.FullName,
            SubjectGroupKey = subject.GroupKey,
            SubjectKey = subjectKey,
            SubjectLabel = label,
            Changes = changes.ToList(),
            Note = note
        });
    }

    private void AddAwardEntries(MemberAward award, Person subject, Person submitter, string submitterRole, Person? reviewer)
    {
        var label = KurinReportNames.AwardLevel(award.Level!.Value);
        AddJournalEntry(
            subject, award.KurinKey, AuditArea.Award, AuditActionNames.Award.Added, award.MemberAwardKey, label,
            AuditChangeRules.Added(
                ("level", award.Level),
                ("dateAcquired", award.DateAcquired),
                ("note", award.Note),
                ("status", BadgeProgressStatus.Submitted)),
            submitter, submitterRole, award.SubmittedAtUtc!.Value);

        if (reviewer is not null && award.ReviewedAtUtc is { } reviewedAt)
        {
            AddJournalEntry(
                subject, award.KurinKey, AuditArea.Award, AuditActionNames.Award.Confirmed, award.MemberAwardKey, label,
                AuditChangeRules.Of(("status", BadgeProgressStatus.Submitted, award.Status)),
                reviewer, ZvyazkovyiRole, reviewedAt);
        }
    }

    private void AddWarningEntries(MemberWarning warning, Person subject, Person issuer, string issuerRole, Person? revoker)
    {
        var label = KurinReportNames.WarningLevel(warning.Level);
        AddJournalEntry(
            subject, warning.KurinKey, AuditArea.Warning, AuditActionNames.Warning.Given, warning.MemberWarningKey, label,
            AuditChangeRules.Added(("level", warning.Level), ("expiresAtUtc", warning.ExpiresAtUtc)),
            issuer, issuerRole, warning.IssuedAtUtc);

        if (revoker is not null && warning.RevokedAtUtc is { } revokedAt)
        {
            AddJournalEntry(
                subject, warning.KurinKey, AuditArea.Warning, AuditActionNames.Warning.Cancelled, warning.MemberWarningKey, label,
                AuditChangeRules.Removed(
                    ("level", warning.Level),
                    ("issuedAtUtc", warning.IssuedAtUtc),
                    ("expiresAtUtc", warning.ExpiresAtUtc)),
                revoker, ZvyazkovyiRole, revokedAt);
        }
    }
}
