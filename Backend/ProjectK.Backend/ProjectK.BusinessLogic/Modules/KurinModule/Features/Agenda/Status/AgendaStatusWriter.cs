using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Status;

/// <summary>
/// Writes one move — a whole target or one person's part — and re-derives what it adds up to, so the
/// target's and the task's stored states never lag behind the part that moved.
/// </summary>
internal static class AgendaStatusWriter
{
    public static void Apply(
        IUnitOfWork uow,
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
                uow.AgendaItems.AddProgress(part);
                // A tracked assignment picks the new row up by fix-up on Add; adding it again would count it twice.
                if (!assignment.Progress.Contains(part))
                {
                    assignment.Progress.Add(part);
                }
            }

            part.Status = status;
            part.ChangedByUserKey = actorUserKey;
            part.ChangedAtUtc = nowUtc;
            assignment.Status = AgendaCompletion.TargetStatus(assignment, roster);
        }
        else
        {
            assignment.Status = status;
        }

        assignment.StatusChangedByUserKey = actorUserKey;
        assignment.StatusChangedAtUtc = nowUtc;
        AgendaCompletion.SetItemStatus(item, AgendaCompletion.ItemStatus(item, roster), nowUtc);
        item.UpdatedDate = nowUtc;
    }

    /// <summary>
    /// Records a move in the kurin's journal: <c>status</c> is what was moved — a part, a target, or
    /// the task when it has no targets — and <c>taskStatus</c> the task as a whole when it moved with
    /// it. <paramref name="movedFrom"/> is null when the task itself was moved. A move that left
    /// everything where it was is not recorded.
    /// </summary>
    public static Task RecordAsync(
        IKurinAudit audit,
        AgendaItem item,
        string label,
        Guid? memberKey,
        AgendaItemStatus? movedFrom,
        AgendaItemStatus movedTo,
        AgendaItemStatus taskBefore,
        CancellationToken cancellationToken)
    {
        var changes = movedFrom is null
            ? AuditChanges.Of(("status", taskBefore, item.Status))
            : AuditChanges.Of(("status", movedFrom, movedTo), ("taskStatus", taskBefore, item.Status));
        if (changes.Count == 0)
        {
            return Task.CompletedTask;
        }

        return audit.RecordAsync(
            new AuditRecord(AuditArea.Agenda, AuditActions.Agenda.StatusChanged, item.AgendaItemKey, label, memberKey, changes, KurinKey: item.KurinKey),
            cancellationToken);
    }

    /// <summary>The author wants to know when the task they set moves as a whole — not when they moved it themselves.</summary>
    public static async Task PublishAsync(IDomainEventPublisher events, AgendaItem item, Guid actorUserKey, CancellationToken cancellationToken)
    {
        if (item.CreatedByUserKey == Guid.Empty || item.CreatedByUserKey == actorUserKey)
        {
            return;
        }

        await events.PublishAsync(
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
