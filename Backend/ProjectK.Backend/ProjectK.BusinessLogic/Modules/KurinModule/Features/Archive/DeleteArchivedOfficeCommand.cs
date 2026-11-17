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

        var office = await _unitOfWork.Archive.FindOfficeAsync(request.KurinKey, request.HistoryKey, cancellationToken);
        if (office is null)
        {
            return ServiceResult<ArchiveDeletionDto>.Failure(ResultType.NotFound, KurinErrors.ArchiveRecordNotFound, "No such holding in this kurin.");
        }

        if (office.EndDate is null)
        {
            return ServiceResult<ArchiveDeletionDto>.Failure(ResultType.Conflict, KurinErrors.ArchiveRecordCurrent, "A current holding is ended on the провід first.");
        }

        if (pass.Reach.IsOwn(office.MemberKey))
        {
            return ServiceResult<ArchiveDeletionDto>.Failure(ResultType.Forbidden, KurinErrors.ArchiveOwnRecord, "Nobody deletes their own record.");
        }

        if (await _gate.SpendAsync(pass, request.KurinKey, cancellationToken) is not { } left)
        {
            return ArchiveDeletionService.Closed();
        }

        var role = KurinRoleNames.Label(office.Role, await _unitOfWork.Archive.GetRoleNamesAsync(request.KurinKey, cancellationToken));
        var holder = (await _members.GetByKeysAsync([office.MemberKey], cancellationToken)).FirstOrDefault()?.FullName;

        await _unitOfWork.Archive.RemoveOfficeAsync(office.HistoryKey, cancellationToken);
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Archive,
                AuditActionNames.Archive.OfficeDeleted,
                office.HistoryKey,
                office.GroupName is { } groupName ? $"{role} · {groupName}" : role,
                office.MemberKey,
                AuditChangeRules.Removed(
                    ("holder", holder),
                    ("role", role),
                    ("group", office.GroupName),
                    ("startDate", office.StartDate),
                    ("endDate", office.EndDate)),
                KurinKey: request.KurinKey),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<ArchiveDeletionDto>(ResultType.Success, new ArchiveDeletionDto(left));
    }
}
