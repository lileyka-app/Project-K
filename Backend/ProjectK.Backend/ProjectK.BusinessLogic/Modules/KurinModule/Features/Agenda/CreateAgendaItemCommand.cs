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

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda;

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
            return ServiceResult<Guid>.Failure(ResultType.Unauthorized, AgendaErrors.AgendaNoActor, "Current user could not be resolved.");
        }

        if (!await _access.CanAuthorAsync(cancellationToken))
        {
            return ServiceResult<Guid>.Failure(ResultType.Forbidden, AgendaErrors.AgendaAuthorForbidden, "The kurin's policy lets this user assign nothing.");
        }

        // Every selected target is checked against the kurin's policy for its kind, so nobody reaches
        // past what the kurin gave them even if the client sends a wider selection.
        foreach (var target in request.Targets)
        {
            var decision = await _access.AuthorizeTargetAsync(target, cancellationToken);
            if (!decision.IsAllowed)
            {
                return ServiceResult<Guid>.Failure(ResultType.Forbidden, AgendaErrors.AgendaTargetForbidden, decision.Reason);
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
                .Select(t => new AgendaAssignment { TargetType = t.TargetType, TargetKey = t.TargetKey, CompletionMode = AgendaTargetModeRules.For(t, request.Kind) })
                .ToList()
        };

        _uow.AgendaItems.Create(item, cancellationToken);

        var targets = AgendaTrail.TargetsOf(item);
        var names = await AgendaTargetNames.LoadAsync(_uow, _members, item.KurinKey, targets, cancellationToken);
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Agenda,
                AuditActions.Agenda.Created,
                item.AgendaItemKey,
                item.Title,
                Changes: AuditChanges.Added([.. AgendaTrail.Of(item), (AgendaTrail.Targets, names.Describe(targets))]),
                KurinKey: item.KurinKey),
            cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        await PublishAssignedAsync(item, actorUserKey.Value, cancellationToken);

        return new ServiceResult<Guid>(ResultType.Created, item.AgendaItemKey);
    }

    private async Task PublishAssignedAsync(AgendaItem item, Guid actorUserKey, CancellationToken cancellationToken)
    {
        var recipients = await AgendaNotificationRecipientRules.ResolveAsync(_uow, _members, item, actorUserKey, cancellationToken);

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

public sealed class CreateAgendaItemCommandValidator : AbstractValidator<CreateAgendaItemCommand>
{
    public CreateAgendaItemCommandValidator()
    {
        RuleFor(command => command.KurinKey).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(2000);
        RuleFor(command => command.Location).MaximumLength(200);
        RuleFor(command => command.Targets).NotEmpty().WithMessage("At least one assignment target is required.");
        RuleForEach(command => command.Targets).ChildRules(target =>
            target.RuleFor(t => t.TargetKey).NotEmpty());

        // An end without a start is meaningless, and an end must not precede a start.
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
