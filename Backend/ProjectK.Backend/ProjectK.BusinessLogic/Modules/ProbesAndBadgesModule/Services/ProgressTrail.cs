using ProjectK.Common.Dtos.AuditModule;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>
/// The status history a проба or a вмілість response carries, read back from the kurin's journal.
/// Only status changes count, as they did when the history had a table of its own: a point signed
/// or unsigned is the journal's business, not the trail's. Each journal action is turned back into
/// the action name and statuses the clients were given before, so the response reads as it did.
/// </summary>
public static class ProgressTrail
{
    private static readonly Dictionary<string, (string Action, ProbeProgressStatus To)> ProbeActions = new(StringComparer.Ordinal)
    {
        [AuditActionNames.Probe.Started] = ("InProgress", ProbeProgressStatus.InProgress),
        [AuditActionNames.Probe.Completed] = ("Completed", ProbeProgressStatus.Completed),
        [AuditActionNames.Probe.Verified] = ("Verified", ProbeProgressStatus.Verified),
        [AuditActionNames.Probe.Unverified] = ("Unverify", ProbeProgressStatus.Completed),
        [AuditActionNames.Probe.Reopened] = ("Unsign", ProbeProgressStatus.InProgress)
    };

    private static readonly Dictionary<string, (string Action, BadgeProgressStatus To)> BadgeActions = new(StringComparer.Ordinal)
    {
        [AuditActionNames.Badge.Submitted] = ("Submitted", BadgeProgressStatus.Submitted),
        [AuditActionNames.Badge.Resubmitted] = ("Resubmitted", BadgeProgressStatus.Submitted),
        [AuditActionNames.Badge.Confirmed] = ("Confirmed", BadgeProgressStatus.Confirmed),
        [AuditActionNames.Badge.Rejected] = ("Rejected", BadgeProgressStatus.Rejected),
        [AuditActionNames.Badge.ConfirmationRemoved] = ("RemovedConfirmed", BadgeProgressStatus.Rejected)
    };

    /// <summary>The status changes of one проба, oldest first, out of <paramref name="journal"/>.</summary>
    public static IReadOnlyCollection<ProbeProgressAuditEventDto> ForProbe(Guid probeProgressKey, IEnumerable<KurinAuditEvent> journal) =>
        journal
            .Where(entry => entry.SubjectKey == probeProgressKey && ProbeActions.ContainsKey(entry.Action))
            .OrderBy(entry => entry.OccurredAtUtc)
            .Select(entry =>
            {
                var (action, to) = ProbeActions[entry.Action];
                var status = StatusChange(entry);
                return new ProbeProgressAuditEventDto(
                    entry.KurinAuditEventKey,
                    Parse<ProbeProgressStatus>(status?.From),
                    Parse<ProbeProgressStatus>(status?.To) ?? to,
                    action,
                    entry.ActorUserKey,
                    entry.ActorName,
                    entry.ActorRole,
                    entry.OccurredAtUtc,
                    entry.Note);
            })
            .ToList();

    /// <summary>The status changes of one вмілість, oldest first, out of <paramref name="journal"/>.</summary>
    public static IReadOnlyCollection<BadgeProgressAuditEventDto> ForBadge(Guid badgeProgressKey, IEnumerable<KurinAuditEvent> journal) =>
        journal
            .Where(entry => entry.SubjectKey == badgeProgressKey && BadgeActions.ContainsKey(entry.Action))
            .OrderBy(entry => entry.OccurredAtUtc)
            .Select(entry =>
            {
                var (action, to) = BadgeActions[entry.Action];
                var status = StatusChange(entry);
                return new BadgeProgressAuditEventDto(
                    entry.KurinAuditEventKey,
                    Parse<BadgeProgressStatus>(status?.From),
                    Parse<BadgeProgressStatus>(status?.To) ?? to,
                    action,
                    entry.ActorUserKey,
                    entry.ActorName,
                    entry.ActorRole,
                    entry.OccurredAtUtc,
                    entry.Note);
            })
            .ToList();

    private static AuditChange? StatusChange(KurinAuditEvent entry) =>
        entry.Changes.FirstOrDefault(change => string.Equals(change.Field, "status", StringComparison.Ordinal));

    private static TStatus? Parse<TStatus>(string? value) where TStatus : struct, Enum =>
        Enum.TryParse<TStatus>(value, ignoreCase: false, out var status) && Enum.IsDefined(status) ? status : null;
}
