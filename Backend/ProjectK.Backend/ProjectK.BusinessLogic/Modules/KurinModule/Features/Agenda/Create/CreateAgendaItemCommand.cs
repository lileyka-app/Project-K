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

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Create;

public sealed record CreateAgendaItemCommand : IRequest<ServiceResult<Guid>>
{
    public Guid KurinKey { get; init; }
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

public sealed class CreateAgendaItemCommandHandler : IRequestHandler<CreateAgendaItemCommand, ServiceResult<Guid>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IAgendaAccess _access;
    private readonly ICurrentUserContext _currentUser;
    private readonly IDomainEventPublisher _events;
    private readonly IKurinAudit _audit;

    public CreateAgendaItemCommandHandler(
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

    public async Task<ServiceResult<Guid>> Handle(CreateAgendaItemCommand request, CancellationToken cancellationToken)
    {
        var actorUserKey = _currentUser.UserId;
        if (actorUserKey is null)
        {
            return ServiceResult<Guid>.Failure(ResultType.Unauthorized, "AGENDA_NO_ACTOR", "Current user could not be resolved.");
        }

        if (!await _access.CanAuthorAsync(cancellationToken))
        {
            return ServiceResult<Guid>.Failure(ResultType.Forbidden, "AGENDA_AUTHOR_FORBIDDEN", "The kurin's policy lets this user assign nothing.");
        }

        // Every selected target is checked against the kurin's policy for its kind, so nobody reaches
        // past what the kurin gave them even if the client sends a wider selection.
        foreach (var target in request.Targets)
        {
            var decision = await _access.AuthorizeTargetAsync(target, cancellationToken);
            if (!decision.IsAllowed)
            {
                return ServiceResult<Guid>.Failure(ResultType.Forbidden, "AGENDA_TARGET_FORBIDDEN", decision.Reason);
            }
        }

        var item = new AgendaItem
        {
            KurinKey = request.KurinKey,
            Kind = request.Kind,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            Status = AgendaItemStatus.Todo,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            IsAllDay = request.IsAllDay,
            AgendaCategoryKey = request.AgendaCategoryKey,
            RecurrenceFrequency = request.RecurrenceFrequency,
            RecurrenceInterval = Math.Max(1, request.RecurrenceInterval),
            RecurrenceByWeekday = request.RecurrenceByWeekday,
            RecurrenceEndUtc = request.RecurrenceEndUtc,
            RecurrenceCount = request.RecurrenceCount,
            CreatedByUserKey = actorUserKey.Value,
            Assignments = request.Targets
                .Select(t => new AgendaAssignment { TargetType = t.TargetType, TargetKey = t.TargetKey, CompletionMode = AgendaTargetModes.For(t, request.Kind) })
                .ToList()
        };

        _uow.AgendaItems.Create(item, cancellationToken);

        var targets = AgendaAuditFields.TargetsOf(item);
        var names = await AgendaTargetNames.LoadAsync(_uow, _members, item.KurinKey, targets, cancellationToken);
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Agenda,
                AuditActions.Agenda.Created,
                item.AgendaItemKey,
                item.Title,
                Changes: AuditChanges.Added([.. AgendaAuditFields.Of(item), (AgendaAuditFields.Targets, names.Describe(targets))]),
                KurinKey: item.KurinKey),
            cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        await PublishAssignedAsync(item, actorUserKey.Value, cancellationToken);

        return new ServiceResult<Guid>(ResultType.Created, item.AgendaItemKey);
    }

    private async Task PublishAssignedAsync(AgendaItem item, Guid actorUserKey, CancellationToken cancellationToken)
    {
        var recipients = await AgendaNotificationRecipients.ResolveAsync(_uow, _members, item, actorUserKey, cancellationToken);

        await _events.PublishAsync(
            new AgendaItemAssigned(
                item.AgendaItemKey,
                item.KurinKey,
                item.Kind,
                item.Title,
                recipients,
                actorUserKey),
            cancellationToken);
    }
}
