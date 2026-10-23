using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Archive;

/// <summary>
/// Puts a task into the archive or takes it back. Whoever may edit the task may do either; a task
/// taken back from the archive returns to the column it was in, with its parts as they were.
/// </summary>
public sealed record SetAgendaItemArchivedCommand(Guid AgendaItemKey, bool Archived) : IRequest<ServiceResult<object>>;

public sealed class SetAgendaItemArchivedCommandHandler : IRequestHandler<SetAgendaItemArchivedCommand, ServiceResult<object>>
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

    public async Task<ServiceResult<object>> Handle(SetAgendaItemArchivedCommand request, CancellationToken cancellationToken)
    {
        var item = await _uow.AgendaItems.GetByKeyWithAssignmentsAsync(request.AgendaItemKey, cancellationToken);
        if (item is null)
        {
            return ServiceResult<object>.Failure(ResultType.NotFound, "AGENDA_NOT_FOUND", "Agenda item was not found.");
        }

        if (item.Kind != AgendaItemKind.Task)
        {
            return ServiceResult<object>.Failure(ResultType.BadRequest, "AGENDA_ARCHIVE_TASKS_ONLY", "Only tasks are archived; events stay in the calendar.");
        }

        var viewer = await _access.BuildViewerAsync(item.KurinKey, cancellationToken);
        if (viewer.KurinKey != _currentUser.KurinKey)
        {
            return ServiceResult<object>.Failure(ResultType.Forbidden, "AGENDA_OTHER_KURIN", "Agenda item belongs to a different kurin.");
        }

        if (!AgendaPermissions.CanManage(item, viewer))
        {
            return ServiceResult<object>.Failure(ResultType.Forbidden, "AGENDA_EDIT_FORBIDDEN", "Only the creator or leadership may archive this task.");
        }

        if (request.Archived == item.ArchivedAtUtc.HasValue)
        {
            return new ServiceResult<object>(ResultType.Success);
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
        return new ServiceResult<object>(ResultType.Success);
    }
}
