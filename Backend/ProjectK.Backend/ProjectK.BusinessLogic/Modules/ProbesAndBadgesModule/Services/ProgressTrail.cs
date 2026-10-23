using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;

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
        [AuditActions.Probe.Started] = ("InProgress", ProbeProgressStatus.InProgress),
        [AuditActions.Probe.Completed] = ("Completed", ProbeProgressStatus.Completed),
        [AuditActions.Probe.Verified] = ("Verified", ProbeProgressStatus.Verified),
        [AuditActions.Probe.Unverified] = ("Unverify", ProbeProgressStatus.Completed),
        [AuditActions.Probe.Reopened] = ("Unsign", ProbeProgressStatus.InProgress)
    };

    private static readonly Dictionary<string, (string Action, BadgeProgressStatus To)> BadgeActions = new(StringComparer.Ordinal)
    {
        [AuditActions.Badge.Submitted] = ("Submitted", BadgeProgressStatus.Submitted),
        [AuditActions.Badge.Resubmitted] = ("Resubmitted", BadgeProgressStatus.Submitted),
        [AuditActions.Badge.Confirmed] = ("Confirmed", BadgeProgressStatus.Confirmed),
        [AuditActions.Badge.Rejected] = ("Rejected", BadgeProgressStatus.Rejected),
        [AuditActions.Badge.ConfirmationRemoved] = ("RemovedConfirmed", BadgeProgressStatus.Rejected)
    };

    /// <summary>The status changes of one проба, oldest first, out of <paramref name="journal"/>.</summary>
    public static IReadOnlyCollection<ProbeProgressAuditEventResponse> ForProbe(Guid probeProgressKey, IEnumerable<KurinAuditEvent> journal) =>
        journal
            .Where(entry => entry.SubjectKey == probeProgressKey && ProbeActions.ContainsKey(entry.Action))
            .OrderBy(entry => entry.OccurredAtUtc)
            .Select(entry =>
            {
                var (action, to) = ProbeActions[entry.Action];
                var status = StatusChange(entry);
                return new ProbeProgressAuditEventResponse(
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
    public static IReadOnlyCollection<BadgeProgressAuditEventResponse> ForBadge(Guid badgeProgressKey, IEnumerable<KurinAuditEvent> journal) =>
        journal
            .Where(entry => entry.SubjectKey == badgeProgressKey && BadgeActions.ContainsKey(entry.Action))
            .OrderBy(entry => entry.OccurredAtUtc)
            .Select(entry =>
            {
                var (action, to) = BadgeActions[entry.Action];
                var status = StatusChange(entry);
                return new BadgeProgressAuditEventResponse(
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
