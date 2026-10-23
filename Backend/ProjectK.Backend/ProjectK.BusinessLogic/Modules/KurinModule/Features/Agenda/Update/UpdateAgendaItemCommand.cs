using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Update;

public sealed record UpdateAgendaItemCommand : IRequest<ServiceResult<object>>
{
    public Guid AgendaItemKey { get; init; }
    public AgendaItemKind Kind { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Location { get; init; }
    public DateTime? StartUtc { get; init; }
    public DateTime? EndUtc { get; init; }
    public bool IsAllDay { get; init; } = true;
    public Guid? AgendaCategoryKey { get; init; }
    public RecurrenceFrequency RecurrenceFrequency { get; init; } = RecurrenceFrequency.None;
    public int RecurrenceInterval { get; init; } = 1;
    public int RecurrenceByWeekday { get; init; }
    public DateTime? RecurrenceEndUtc { get; init; }
    public int? RecurrenceCount { get; init; }
    public List<AgendaTargetInput> Targets { get; init; } = [];
}

public sealed class UpdateAgendaItemCommandHandler : IRequestHandler<UpdateAgendaItemCommand, ServiceResult<object>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IAgendaAccess _access;
    private readonly ICurrentUserContext _currentUser;
    private readonly IDomainEventPublisher _events;
    private readonly IKurinAudit _audit;

    public UpdateAgendaItemCommandHandler(
        IUnitOfWork uow,
        IMemberDirectory members,
        IAgendaAccess access,
        ICurrentUserContext currentUser,
        IDomainEventPublisher events,
        IKurinAudit audit)
    {
        _uow = uow;
        _members = members;
        _access = access;
        _currentUser = currentUser;
        _events = events;
        _audit = audit;
    }

    public async Task<ServiceResult<object>> Handle(UpdateAgendaItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _uow.AgendaItems.GetByKeyWithAssignmentsAsync(request.AgendaItemKey, cancellationToken);
        if (item is null)
        {
            return ServiceResult<object>.Failure(ResultType.NotFound, "AGENDA_NOT_FOUND", "Agenda item was not found.");
        }

        var viewer = await _access.BuildViewerAsync(item.KurinKey, cancellationToken);
        if (viewer.KurinKey != _currentUser.KurinKey)
        {
            return ServiceResult<object>.Failure(ResultType.Forbidden, "AGENDA_OTHER_KURIN", "Agenda item belongs to a different kurin.");
        }

        if (!AgendaPermissions.CanManage(item, viewer))
        {
            return ServiceResult<object>.Failure(ResultType.Forbidden, "AGENDA_EDIT_FORBIDDEN", "Only the creator or leadership may edit this item.");
        }

        // Only a newly added target is an act of assigning: one the item already has stays when the
        // kurin narrows the editor's reach, so a title can still be fixed on an item aimed further.
        var existingTargets = item.Assignments.Select(a => (a.TargetType, a.TargetKey)).ToHashSet();
        foreach (var target in request.Targets.Where(t => !existingTargets.Contains((t.TargetType, t.TargetKey))))
        {
            var decision = await _access.AuthorizeTargetAsync(target, cancellationToken);
            if (!decision.IsAllowed)
            {
                return ServiceResult<object>.Failure(ResultType.Forbidden, "AGENDA_TARGET_FORBIDDEN", decision.Reason);
            }
        }

        // Switching «кожному окремо» to a shared mode would silently drop everyone's parts, and the
        // reverse would leave a closed shared state nobody did; once a target has moved its mode stays.
        var currentModes = item.Assignments.ToDictionary(a => (a.TargetType, a.TargetKey), a => a.CompletionMode);
        var requestedModes = request.Targets
            .GroupBy(t => (t.TargetType, t.TargetKey))
            .ToDictionary(
                g => g.Key,
                g => AgendaTargetModes.For(g.First(), request.Kind, currentModes.TryGetValue(g.Key, out var current) ? current : null));
        var reshaped = item.Assignments.FirstOrDefault(a =>
            requestedModes.TryGetValue((a.TargetType, a.TargetKey), out var mode)
            && mode != a.CompletionMode
            && AgendaCompletion.HasMoved(a));
        if (reshaped is not null)
        {
            return ServiceResult<object>.Failure(ResultType.Conflict, "AGENDA_MODE_LOCKED",
                "The way this target is done cannot change once someone has moved it; create a new task instead.");
        }

        var before = AgendaAuditFields.Of(item);
        var targetsBefore = AgendaAuditFields.TargetsOf(item);
        var statusBefore = item.Status;

        item.Kind = request.Kind;
        item.Title = request.Title.Trim();
        item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        item.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        item.StartUtc = request.StartUtc;
        item.EndUtc = request.EndUtc;
        item.IsAllDay = request.IsAllDay;
        item.AgendaCategoryKey = request.AgendaCategoryKey;
        item.RecurrenceFrequency = request.RecurrenceFrequency;
        item.RecurrenceInterval = Math.Max(1, request.RecurrenceInterval);
        item.RecurrenceByWeekday = request.RecurrenceByWeekday;
        item.RecurrenceEndUtc = request.RecurrenceEndUtc;
        item.RecurrenceCount = request.RecurrenceCount;
        item.UpdatedDate = DateTime.UtcNow;

        // Reconcile targets: keep unchanged rows, delete removed ones, insert new ones — via explicit
        // DbSet Add/Remove so EF gets unambiguous Added/Deleted states (mutating the tracked collection
        // alone made EF mark a new assignment Modified and emit an UPDATE that matched no row). The tracked
        // collection is kept in sync too, so the item stays clean for the notification's own SaveChanges.
        ReconcileTargets(item, request.Targets, requestedModes);

        // Targets came and went, so what the task adds up to may have changed with them.
        var roster = await AgendaRoster.LoadAsync(_uow, _members, item.KurinKey, [item], cancellationToken);
        AgendaCompletion.SetItemStatus(item, AgendaCompletion.ItemStatus(item, roster), DateTime.UtcNow);

        await RecordChangesAsync(item, before, targetsBefore, statusBefore, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        await PublishChangedAsync(item, viewer.ViewerUserKey ?? Guid.Empty, cancellationToken);

        return new ServiceResult<object>(ResultType.Success);
    }

    /// <summary>Records what the edit changed, targets and the status they moved included; an edit that changed nothing is not recorded.</summary>
    private async Task RecordChangesAsync(
        AgendaItem item,
        (string Field, object? Value)[] before,
        List<(AgendaTargetType Type, Guid Key)> targetsBefore,
        AgendaItemStatus statusBefore,
        CancellationToken cancellationToken)
    {
        var targetsAfter = AgendaAuditFields.TargetsOf(item);
        var changes = AgendaAuditFields.Between(before, AgendaAuditFields.Of(item)).ToList();
        if (!targetsBefore.ToHashSet().SetEquals(targetsAfter))
        {
            var names = await AgendaTargetNames.LoadAsync(_uow, _members, item.KurinKey, targetsBefore.Concat(targetsAfter), cancellationToken);
            changes.AddRange(AuditChanges.Of((AgendaAuditFields.Targets, names.Describe(targetsBefore), names.Describe(targetsAfter))));
        }

        changes.AddRange(AuditChanges.Of(("status", statusBefore, item.Status)));
        if (changes.Count == 0)
        {
            return;
        }

        await _audit.RecordAsync(
            new AuditRecord(AuditArea.Agenda, AuditActions.Agenda.Changed, item.AgendaItemKey, item.Title, Changes: changes, KurinKey: item.KurinKey),
            cancellationToken);
    }

    private void ReconcileTargets(
        AgendaItem item,
        List<AgendaTargetInput> targets,
        Dictionary<(AgendaTargetType, Guid), AgendaCompletionMode> requestedModes)
    {
        var desired = targets
            .Select(t => (t.TargetType, t.TargetKey))
            .ToHashSet();

        var kept = new HashSet<(AgendaTargetType, Guid)>();
        foreach (var existing in item.Assignments.ToList())
        {
            var tuple = (existing.TargetType, existing.TargetKey);
            if (desired.Contains(tuple))
            {
                kept.Add(tuple);
                existing.CompletionMode = requestedModes[tuple];
            }
            else
            {
                _uow.AgendaItems.RemoveAssignment(existing);
                item.Assignments.Remove(existing);
            }
        }

        foreach (var target in targets)
        {
            if (kept.Add((target.TargetType, target.TargetKey)))
            {
                var assignment = new AgendaAssignment
                {
                    AgendaItemKey = item.AgendaItemKey,
                    TargetType = target.TargetType,
                    TargetKey = target.TargetKey,
                    CompletionMode = requestedModes[(target.TargetType, target.TargetKey)]
                };
                _uow.AgendaItems.AddAssignment(assignment);
                item.Assignments.Add(assignment);
            }
        }
    }

    private async Task PublishChangedAsync(AgendaItem item, Guid actorUserKey, CancellationToken cancellationToken)
    {
        var recipients = await AgendaNotificationRecipients.ResolveAsync(_uow, _members, item, actorUserKey, cancellationToken);

        await _events.PublishAsync(
            new AgendaItemChanged(
                item.AgendaItemKey,
                item.KurinKey,
                item.Kind,
                item.Title,
                recipients,
                actorUserKey),
            cancellationToken);
    }
}
