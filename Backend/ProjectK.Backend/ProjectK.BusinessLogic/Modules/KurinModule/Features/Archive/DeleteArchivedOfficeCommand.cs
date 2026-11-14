using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Archive;

/// <summary>
/// Deletes one former holding of an office for good, in the danger zone. The journal keeps what it
/// was: who, which role, from when to when.
/// </summary>
public sealed record DeleteArchivedOfficeCommand(Guid KurinKey, Guid HistoryKey, string? Ticket) : IRequest<ServiceResult<ArchiveDeletionDto>>;

public sealed class DeleteArchivedOfficeCommandHandler : IRequestHandler<DeleteArchivedOfficeCommand, ServiceResult<ArchiveDeletionDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly ArchiveDeletionService _gate;
    private readonly IKurinAudit _audit;

    public DeleteArchivedOfficeCommandHandler(IUnitOfWork unitOfWork, IMemberDirectory members, ArchiveDeletionService gate, IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _gate = gate;
        _audit = audit;
    }

    public async Task<ServiceResult<ArchiveDeletionDto>> Handle(DeleteArchivedOfficeCommand request, CancellationToken cancellationToken)
    {
        var (pass, refusal) = await _gate.OpenAsync(request.KurinKey, request.Ticket, cancellationToken);
        if (pass is null)
        {
            return refusal!;
        }

        var history = await _unitOfWork.Archive.FindOfficeAsync(request.HistoryKey, cancellationToken);
        var leadership = history?.Leadership;
        if (history is null || (leadership!.KurinKey ?? leadership.Group?.KurinKey) != request.KurinKey)
        {
            return ServiceResult<ArchiveDeletionDto>.Failure(ResultType.NotFound, ArchiveErrors.NotFound, "No such holding in this kurin.");
        }

        if (history.EndDate is null)
        {
            return ServiceResult<ArchiveDeletionDto>.Failure(ResultType.Conflict, ArchiveErrors.StillCurrent, "A current holding is ended on the провід first.");
        }

        if (pass.Reach.IsOwn(history.MemberKey))
        {
            return ServiceResult<ArchiveDeletionDto>.Failure(ResultType.Forbidden, ArchiveErrors.OwnRecord, "Nobody deletes their own record.");
        }

        if (await _gate.SpendAsync(pass, request.KurinKey, cancellationToken) is not { } left)
        {
            return ArchiveDeletionService.Closed();
        }

        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        var role = ArchiveErrors.RoleLabel(history.Role, KurinRoleNames.Read(kurin?.RoleNames));
        var holder = (await _members.GetByKeysAsync([history.MemberKey], cancellationToken)).FirstOrDefault()?.FullName;

        _unitOfWork.Archive.RemoveOffice(history);
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Archive,
                AuditActions.Archive.OfficeDeleted,
                history.LeadershipHistoryKey,
                leadership.Group?.Name is { } groupName ? $"{role} · {groupName}" : role,
                history.MemberKey,
                AuditChanges.Removed(
                    ("holder", holder),
                    ("role", role),
                    ("group", leadership.Group?.Name),
                    ("startDate", history.StartDate),
                    ("endDate", history.EndDate)),
                KurinKey: request.KurinKey),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<ArchiveDeletionDto>(ResultType.Success, new ArchiveDeletionDto(left));
    }
}
