using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda;

public sealed record DeleteAgendaItemCommand(Guid AgendaItemKey) : IRequest<ServiceResult<Unit>>;

public sealed class DeleteAgendaItemCommandHandler : IRequestHandler<DeleteAgendaItemCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IAgendaAccess _access;
    private readonly ICurrentUserContext _currentUser;
    private readonly IDomainEventPublisher _events;
    private readonly IKurinAudit _audit;

    public DeleteAgendaItemCommandHandler(
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

    public async Task<ServiceResult<Unit>> Handle(DeleteAgendaItemCommand request, CancellationToken cancellationToken)
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
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, AgendaErrors.AgendaDeleteForbidden, "Only the creator or leadership may delete this item.");
        }

        // Resolve who was assigned before the row (and its assignments) are gone.
        var actorUserKey = viewer.ViewerUserKey ?? Guid.Empty;
        var recipients = await AgendaNotificationRecipientRules.ResolveAsync(_uow, _members, item, actorUserKey, cancellationToken);
        var title = item.Title;
        var kind = item.Kind;
        var kurinKey = item.KurinKey;

        var targets = AgendaTrail.TargetsOf(item);
        var names = await AgendaTargetNames.LoadAsync(_uow, _members, kurinKey, targets, cancellationToken);
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Agenda,
                AuditActions.Agenda.Deleted,
                item.AgendaItemKey,
                title,
                Changes: AuditChanges.Removed([.. AgendaTrail.Of(item), (AgendaTrail.Targets, names.Describe(targets))]),
                KurinKey: kurinKey),
            cancellationToken);

        // Assignments are removed by the cascade delete configured on the relationship.
        _uow.AgendaItems.Delete(item, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        await _events.PublishAsync(
            new AgendaItemRemoved(
                request.AgendaItemKey,
                kurinKey,
                kind,
                title,
                recipients,
                actorUserKey),
            cancellationToken);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
