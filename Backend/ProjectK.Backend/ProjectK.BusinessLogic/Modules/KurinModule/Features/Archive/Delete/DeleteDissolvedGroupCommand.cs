using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Archive.Delete;

/// <summary>
/// Deletes a dissolved гурток for good, in the danger zone, with its провід, that провід's history
/// and the stretches people spent in it. A standing гурток is dissolved first.
/// </summary>
public sealed record DeleteDissolvedGroupCommand(Guid KurinKey, Guid GroupKey, string? Ticket) : IRequest<ServiceResult<ArchiveDeletionDto>>;

public sealed class DeleteDissolvedGroupCommandHandler : IRequestHandler<DeleteDissolvedGroupCommand, ServiceResult<ArchiveDeletionDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ArchiveDeletionGate _gate;
    private readonly IKurinAudit _audit;

    public DeleteDissolvedGroupCommandHandler(IUnitOfWork unitOfWork, ArchiveDeletionGate gate, IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _gate = gate;
        _audit = audit;
    }

    public async Task<ServiceResult<ArchiveDeletionDto>> Handle(DeleteDissolvedGroupCommand request, CancellationToken cancellationToken)
    {
        var (pass, refusal) = await _gate.OpenAsync(request.KurinKey, request.Ticket, cancellationToken);
        if (pass is null)
        {
            return refusal!;
        }

        var group = await _unitOfWork.Archive.FindDissolvedGroupAsync(request.GroupKey, cancellationToken);
        if (group is null || group.KurinKey != request.KurinKey)
        {
            return ServiceResult<ArchiveDeletionDto>.Failure(ResultType.NotFound, ArchiveErrors.NotFound, "No such dissolved гурток in this kurin.");
        }

        if (await _gate.SpendAsync(pass, request.KurinKey, cancellationToken) is not { } left)
        {
            return ArchiveDeletionGate.Closed();
        }

        var holdings = await _unitOfWork.Archive.RemoveDissolvedGroupAsync(group, cancellationToken);
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Archive,
                AuditActions.Archive.GroupDeleted,
                group.GroupKey,
                group.Name,
                Changes: AuditChanges.Removed(
                    ("group", group.Name),
                    ("dissolvedOn", group.DissolvedAtUtc is { } dissolved ? DateOnly.FromDateTime(dissolved) : null),
                    ("offices", holdings == 0 ? null : holdings)),
                KurinKey: request.KurinKey),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<ArchiveDeletionDto>(ResultType.Success, new ArchiveDeletionDto(left));
    }
}
