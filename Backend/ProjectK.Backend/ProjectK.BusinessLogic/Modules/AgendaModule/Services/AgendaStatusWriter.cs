using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Events.AgendaModule;
using ProjectK.Common.Records.AgendaModule;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Rules.AgendaModule;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Services;

/// <summary>
/// Writes one move — a whole target or one person's part — and re-derives what it adds up to, so the
/// target's and the task's stored states never lag behind the part that moved.
/// </summary>
public sealed class AgendaStatusWriter
{
    private readonly IUnitOfWork _uow;
    private readonly IKurinAudit _audit;
    private readonly IDomainEventPublisher _events;

    public AgendaStatusWriter(IUnitOfWork uow, IKurinAudit audit, IDomainEventPublisher events)
    {
        _uow = uow;
        _audit = audit;
        _events = events;
    }

    public void Apply(
        AgendaItem item,
        AgendaStake stake,
        AgendaItemStatus status,
        Guid actorUserKey,
        AgendaRoster roster,
        DateTime nowUtc)
    {
        var assignment = stake.Assignment;
        if (stake.MemberKey is { } memberKey)
        {
            var part = assignment.Progress.FirstOrDefault(p => p.MemberKey == memberKey);
            if (part is null)
            {
                part = new AgendaAssignmentProgress
                {
                    AgendaAssignmentKey = assignment.AgendaAssignmentKey,
                    MemberKey = memberKey
                };
                _uow.AgendaItems.AddProgress(part);
                // A tracked assignment picks the new row up by fix-up on Add; adding it again would count it twice.
                if (!assignment.Progress.Contains(part))
                {
                    assignment.Progress.Add(part);
                }
            }

            part.Status = status;
            part.ChangedByUserKey = actorUserKey;
            part.ChangedAtUtc = nowUtc;
            assignment.Status = AgendaCompletionRules.TargetStatus(assignment, roster);
        }
        else
        {
            assignment.Status = status;
        }

        assignment.StatusChangedByUserKey = actorUserKey;
        assignment.StatusChangedAtUtc = nowUtc;
        AgendaCompletionRules.SetItemStatus(item, AgendaCompletionRules.ItemStatus(item, roster), nowUtc);
        item.UpdatedDate = nowUtc;
    }

    /// <summary>
    /// Records a move in the kurin's journal: <c>status</c> is what was moved — a part, a target, or
    /// the task when it has no targets — and <c>taskStatus</c> the task as a whole when it moved with
    /// it. <paramref name="movedFrom"/> is null when the task itself was moved. A move that left
    /// everything where it was is not recorded.
    /// </summary>
    public Task RecordAsync(
        AgendaItem item,
        string label,
        Guid? memberKey,
        AgendaItemStatus? movedFrom,
        AgendaItemStatus movedTo,
        AgendaItemStatus taskBefore,
        CancellationToken cancellationToken)
    {
        var changes = movedFrom is null
            ? AuditChangeRules.Of(("status", taskBefore, item.Status))
            : AuditChangeRules.Of(("status", movedFrom, movedTo), ("taskStatus", taskBefore, item.Status));
        if (changes.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _audit.RecordAsync(
            new AuditRecord(AuditArea.Agenda, AuditActionNames.Agenda.StatusChanged, item.AgendaItemKey, label, memberKey, changes, KurinKey: item.KurinKey),
            cancellationToken);
    }

    /// <summary>The author wants to know when the task they set moves as a whole — not when they moved it themselves.</summary>
    public async Task PublishAsync(AgendaItem item, Guid actorUserKey, CancellationToken cancellationToken)
    {
        if (item.CreatedByUserKey == Guid.Empty || item.CreatedByUserKey == actorUserKey)
        {
            return;
        }

        await _events.PublishAsync(
            new AgendaItemStatusChanged(
                item.AgendaItemKey,
                item.KurinKey,
                item.Kind,
                item.Title,
                item.Status,
                item.CreatedByUserKey,
                actorUserKey),
            cancellationToken);
    }
}
