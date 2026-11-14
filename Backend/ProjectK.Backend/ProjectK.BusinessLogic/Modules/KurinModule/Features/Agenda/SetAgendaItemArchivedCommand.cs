using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda;

/// <summary>
/// Puts a task into the archive or takes it back. Whoever may edit the task may do either; a task
/// taken back from the archive returns to the column it was in, with its parts as they were.
/// </summary>
public sealed record SetAgendaItemArchivedCommand(Guid AgendaItemKey, bool Archived) : IRequest<ServiceResult<Unit>>;

public sealed class SetAgendaItemArchivedCommandHandler : IRequestHandler<SetAgendaItemArchivedCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _uow;
    private readonly IAgendaAccess _access;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;
    private readonly IKurinAudit _audit;

    public SetAgendaItemArchivedCommandHandler(IUnitOfWork uow, IAgendaAccess access, ICurrentUserContext currentUser, TimeProvider time, IKurinAudit audit)
    {
        _uow = uow;
        _access = access;
        _currentUser = currentUser;
        _time = time;
        _audit = audit;
    }

    public async Task<ServiceResult<Unit>> Handle(SetAgendaItemArchivedCommand request, CancellationToken cancellationToken)
    {
        var item = await _uow.AgendaItems.GetByKeyWithAssignmentsAsync(request.AgendaItemKey, cancellationToken);
        if (item is null)
        {
            return AgendaErrors.NotFound<Unit>();
        }

        if (item.Kind != AgendaItemKind.Task)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, AgendaErrors.AgendaArchiveTasksOnly, "Only tasks are archived; events stay in the calendar.");
        }

        var viewer = await _access.BuildViewerAsync(item.KurinKey, cancellationToken);
        if (viewer.KurinKey != _currentUser.KurinKey)
        {
            return AgendaErrors.OtherKurin<Unit>();
        }

        if (!AgendaPermissions.CanManage(item, viewer))
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, AgendaErrors.AgendaEditForbidden, "Only the creator or leadership may archive this task.");
        }

        if (request.Archived == item.ArchivedAtUtc.HasValue)
        {
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        var now = _time.GetUtcNow().UtcDateTime;
        item.ArchivedAtUtc = request.Archived ? now : null;
        item.ArchivedByUserKey = request.Archived ? viewer.ViewerUserKey : null;
        // Taken back, a done task would otherwise be swept into the archive again the same night.
        if (!request.Archived && item.CompletedAtUtc.HasValue)
        {
            item.CompletedAtUtc = now;
        }

        item.UpdatedDate = now;
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Agenda,
                request.Archived ? AuditActions.Agenda.Archived : AuditActions.Agenda.Restored,
                item.AgendaItemKey,
                item.Title,
                KurinKey: item.KurinKey),
            cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
