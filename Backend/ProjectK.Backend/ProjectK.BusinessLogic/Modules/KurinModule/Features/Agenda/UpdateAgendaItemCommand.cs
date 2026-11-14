using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda;

public sealed record UpdateAgendaItemCommand : IRequest<ServiceResult<Unit>>
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

public sealed class UpdateAgendaItemCommandHandler : IRequestHandler<UpdateAgendaItemCommand, ServiceResult<Unit>>
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

    public async Task<ServiceResult<Unit>> Handle(UpdateAgendaItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _uow.AgendaItems.GetByKeyWithAssignmentsAsync(request.AgendaItemKey, cancellationToken);
        if (item is null)
        {
            return AgendaErrors.NotFound<Unit>();
        }

        var viewer = await _access.BuildViewerAsync(item.KurinKey, cancellationToken);
        if (viewer.KurinKey != _currentUser.KurinKey)
        {
            return AgendaErrors.OtherKurin<Unit>();
        }

        if (!AgendaAccess.CanManage(item, viewer))
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, AgendaErrors.AgendaEditForbidden, "Only the creator or leadership may edit this item.");
        }

        // Only a newly added target is an act of assigning: one the item already has stays when the
        // kurin narrows the editor's reach, so a title can still be fixed on an item aimed further.
        var existingTargets = item.Assignments.Select(a => (a.TargetType, a.TargetKey)).ToHashSet();
        foreach (var target in request.Targets.Where(t => !existingTargets.Contains((t.TargetType, t.TargetKey))))
        {
            var decision = await _access.AuthorizeTargetAsync(target, cancellationToken);
            if (!decision.IsAllowed)
            {
                return ServiceResult<Unit>.Failure(ResultType.Forbidden, AgendaErrors.AgendaTargetForbidden, decision.Reason);
            }
        }

        // Switching «кожному окремо» to a shared mode would silently drop everyone's parts, and the
        // reverse would leave a closed shared state nobody did; once a target has moved its mode stays.
        var currentModes = item.Assignments.ToDictionary(a => (a.TargetType, a.TargetKey), a => a.CompletionMode);
        var requestedModes = request.Targets
            .GroupBy(t => (t.TargetType, t.TargetKey))
            .ToDictionary(
                g => g.Key,
                g => AgendaTargetModeRules.For(g.First(), request.Kind, currentModes.TryGetValue(g.Key, out var current) ? current : null));
        var reshaped = item.Assignments.FirstOrDefault(a =>
            requestedModes.TryGetValue((a.TargetType, a.TargetKey), out var mode)
            && mode != a.CompletionMode
            && AgendaCompletionRules.HasMoved(a));
        if (reshaped is not null)
        {
            return ServiceResult<Unit>.Failure(ResultType.Conflict, AgendaErrors.AgendaModeLocked,
                "The way this target is done cannot change once someone has moved it; create a new task instead.");
        }

        var before = AgendaTrail.Of(item);
        var targetsBefore = AgendaTrail.TargetsOf(item);
        var statusBefore = item.Status;

        item.Kind = request.Kind;
        item.Title = request.Title.Trim();
        item.Description = TextRules.TrimOrNull(request.Description);
        item.Location = TextRules.TrimOrNull(request.Location);
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
        AgendaCompletionRules.SetItemStatus(item, AgendaCompletionRules.ItemStatus(item, roster), DateTime.UtcNow);

        await RecordChangesAsync(item, before, targetsBefore, statusBefore, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        await PublishChangedAsync(item, viewer.ViewerUserKey ?? Guid.Empty, cancellationToken);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }

    /// <summary>Records what the edit changed, targets and the status they moved included; an edit that changed nothing is not recorded.</summary>
    private async Task RecordChangesAsync(
        AgendaItem item,
        (string Field, object? Value)[] before,
        List<(AgendaTargetType Type, Guid Key)> targetsBefore,
        AgendaItemStatus statusBefore,
        CancellationToken cancellationToken)
    {
        var targetsAfter = AgendaTrail.TargetsOf(item);
        var changes = AgendaTrail.Between(before, AgendaTrail.Of(item)).ToList();
        if (!targetsBefore.ToHashSet().SetEquals(targetsAfter))
        {
            var names = await AgendaTargetNames.LoadAsync(_uow, _members, item.KurinKey, targetsBefore.Concat(targetsAfter), cancellationToken);
            changes.AddRange(AuditChangeRules.Of((AgendaTrail.Targets, names.Describe(targetsBefore), names.Describe(targetsAfter))));
        }

        changes.AddRange(AuditChangeRules.Of(("status", statusBefore, item.Status)));
        if (changes.Count == 0)
        {
            return;
        }

        await _audit.RecordAsync(
            new AuditRecord(AuditArea.Agenda, AuditActionNames.Agenda.Changed, item.AgendaItemKey, item.Title, Changes: changes, KurinKey: item.KurinKey),
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
        var recipients = await AgendaNotificationRecipientRules.ResolveAsync(_uow, _members, item, actorUserKey, cancellationToken);

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

public sealed class UpdateAgendaItemCommandValidator : AbstractValidator<UpdateAgendaItemCommand>
{
    public UpdateAgendaItemCommandValidator()
    {
        RuleFor(command => command.AgendaItemKey).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(2000);
        RuleFor(command => command.Location).MaximumLength(200);
        RuleFor(command => command.Targets).NotEmpty().WithMessage("At least one assignment target is required.");
        RuleForEach(command => command.Targets).ChildRules(target =>
            target.RuleFor(t => t.TargetKey).NotEmpty());

        RuleFor(command => command)
            .Must(c => !(c.EndUtc.HasValue && c.StartUtc is null))
            .WithMessage("EndUtc requires StartUtc.");
        RuleFor(command => command)
            .Must(c => !(c.StartUtc.HasValue && c.EndUtc.HasValue) || c.EndUtc.Value >= c.StartUtc.Value)
            .WithMessage("EndUtc must not be before StartUtc.");

        RuleFor(c => c.RecurrenceInterval).GreaterThanOrEqualTo(1);
        RuleFor(c => c.RecurrenceCount).GreaterThan(0).When(c => c.RecurrenceCount.HasValue);
        RuleFor(c => c)
            .Must(c => c.RecurrenceFrequency == RecurrenceFrequency.None || c.StartUtc.HasValue)
            .WithMessage("A recurring item needs a start date.");
        RuleFor(c => c)
            .Must(c => !(c.RecurrenceEndUtc.HasValue && c.StartUtc.HasValue) || c.RecurrenceEndUtc.Value >= c.StartUtc.Value)
            .WithMessage("RecurrenceEndUtc must not be before StartUtc.");
    }
}
