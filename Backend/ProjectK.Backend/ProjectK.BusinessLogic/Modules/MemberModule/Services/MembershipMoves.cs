using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Events.MemberModule;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.AuditModule;
using MembershipEntity = ProjectK.Common.Entities.MemberModule.Membership;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Services;

/// <summary>
/// Opening and closing a membership the way every path does it: the row, the kurin's journal and,
/// for a closing, the word to whoever counts time in the kurin. Nothing here saves — the caller saves
/// once, so a transfer that closes one kurin and opens another commits as one change.
/// </summary>
public sealed class MembershipMoves
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKurinAudit _audit;
    private readonly IDomainEventPublisher _events;
    private readonly IDuesDirectory _dues;
    private readonly TimeProvider _time;

    public MembershipMoves(IUnitOfWork unitOfWork, IKurinAudit audit, IDomainEventPublisher events, IDuesDirectory dues, TimeProvider time)
    {
        _time = time;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _events = events;
        _dues = dues;
    }

    /// <summary>
    /// What keeps a person in a kurin: dues they still owe it for quarters already behind them, over
    /// every stretch of theirs there — an account from a гурток they stood in years ago included. Debt
    /// does not leave with the person.
    /// </summary>
    public async Task<decimal> OwedToLeaveAsync(Guid memberKey, Guid kurinKey, CancellationToken cancellationToken)
    {
        var stretchesHere = (await _unitOfWork.Memberships.GetRecordsForMemberAsync(memberKey, cancellationToken))
            .Where(m => m.KurinKey == kurinKey)
            .Select(m => m.MembershipKey)
            .ToList();
        return await _dues.GetOwedAsync(kurinKey, stretchesHere, cancellationToken);
    }

    /// <summary>
    /// Closes a membership now, recorded while the гурток is still known. What other kurins still ask
    /// this one about the person lapses: there is nobody here to let go any more.
    /// </summary>
    public async Task CloseAsync(MembershipEntity membership, string personName, CancellationToken cancellationToken)
    {
        membership.LeftAtUtc = DateTime.UtcNow;

        var now = _time.GetUtcNow().UtcDateTime;
        // A request answered in this same unit of work is still pending in the database, not here.
        foreach (var asked in (await _unitOfWork.MembershipRequests.GetPendingFromAsync(membership.MemberKey, membership.KurinKey, cancellationToken))
                     .Where(r => r.Status == MembershipRequestStatus.Pending))
        {
            asked.Lapse(now);
        }

        var group = membership.GroupKey is { } groupKey
            ? await _unitOfWork.Groups.GetByKeyAsync(groupKey, cancellationToken)
            : null;
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Membership,
                AuditActionNames.Membership.Left,
                membership.MembershipKey,
                personName,
                membership.MemberKey,
                AuditChangeRules.Removed(("kind", membership.Kind), ("group", group?.Name)),
                KurinKey: membership.KurinKey),
            cancellationToken);

        // Announced before the save, so what the dues module settles for the quarter they left in
        // commits together with the closed membership.
        await _events.PublishAsync(
            new MembershipLeft(membership.MembershipKey, membership.KurinKey, membership.LeftAtUtc.Value),
            cancellationToken);
    }

    /// <summary>Opens a membership in a kurin, outside any гурток.</summary>
    public async Task<MembershipEntity> OpenAsync(MemberSummary person, Guid kurinKey, MembershipKind kind, CancellationToken cancellationToken)
    {
        var membership = new MembershipEntity
        {
            MemberKey = person.MemberKey,
            UserKey = person.UserKey,
            KurinKey = kurinKey,
            Kind = kind,
            JoinedAtUtc = DateTime.UtcNow
        };

        _unitOfWork.Memberships.Open(membership);
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Membership,
                AuditActionNames.Membership.Joined,
                membership.MembershipKey,
                person.FullName,
                person.MemberKey,
                AuditChangeRules.Added(("kind", kind), ("group", (string?)null)),
                KurinKey: kurinKey),
            cancellationToken);
        return membership;
    }
}
