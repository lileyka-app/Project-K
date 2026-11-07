using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Delete;

/// <summary>
/// Dissolves a гурток. Nothing is erased: the гурток, its провід and that провід's history move to
/// the kurin's archive, and only the danger zone deletes them for good.
/// </summary>
public sealed record DeleteGroupCommand(Guid GroupKey) : IRequest<ServiceResult<object>>;

public sealed class DeleteGroupCommandHandler : IRequestHandler<DeleteGroupCommand, ServiceResult<object>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackendCache _cache;
    private readonly IKurinAudit _audit;
    private readonly TimeProvider _time;
    private readonly IDuesDirectory _dues;

    public DeleteGroupCommandHandler(IUnitOfWork unitOfWork, IBackendCache cache, IKurinAudit audit, TimeProvider time, IDuesDirectory dues)
    {
        _dues = dues;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _audit = audit;
        _time = time;
    }

    public async Task<ServiceResult<object>> Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
    {
        if (request.GroupKey == Guid.Empty)
        {
            return ServiceResult<object>.Failure(
                ResultType.BadRequest,
                "GROUP_KEY_EMPTY",
                "GroupKey cannot be empty.");
        }
        var existing = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey, cancellationToken);
        if (existing is null)
        {
            return ServiceResult<object>.Failure(
                ResultType.NotFound,
                "GROUP_NOT_FOUND",
                $"Group with key {request.GroupKey} not found.");
        }

        // A dissolved гурток's box is no longer read anywhere: money left in it would be stuck.
        if (!await _dues.GroupBoxSettledAsync(existing.KurinKey, existing.GroupKey, cancellationToken))
        {
            return ServiceResult<object>.Failure(
                ResultType.Conflict,
                ArchiveErrors.DissolveWithDues,
                "The гурток's box still holds money, owes the kurin or has accounts in debt.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var leadershipKeys = await _unitOfWork.Leaderships.CloseForGroupAsync(request.GroupKey, DateOnly.FromDateTime(now), cancellationToken);

        // A впорядник of a dissolved гурток leads nothing; the revoked row stays as history.
        foreach (var assignment in await _unitOfWork.MentorAssignments.GetByGroupKeyAsync(request.GroupKey, cancellationToken))
        {
            assignment.RevokedAtUtc ??= now;
        }

        // The people stay, and stay in the kurin — they are simply no longer in a гурток, which
        // is a state a membership is allowed to be in. Dissolving a гурток is not a reason for
        // anyone to leave.
        await _unitOfWork.Memberships.DetachFromGroupAsync(request.GroupKey, cancellationToken);

        // Agenda assignments name their target by a bare key, so nothing in the database clears
        // them: neither the гурток nor its offices can be a target any more.
        await _unitOfWork.AgendaItems.RemoveAssignmentsForTargetsAsync(
            [request.GroupKey, .. leadershipKeys],
            cancellationToken);

        existing.DissolvedAtUtc = now;
        await _audit.RecordAsync(
            new AuditRecord(AuditArea.Archive, AuditActions.Archive.GroupDissolved, existing.GroupKey, existing.Name, KurinKey: existing.KurinKey),
            cancellationToken);

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return ServiceResult<object>.Failure(
                ResultType.InternalServerError,
                "GROUP_DELETE_FAILED",
                "Failed to delete Group due to internal error.");
        }
        _cache.Invalidate(BackendCachePolicies.GroupReads);
        _cache.Invalidate(BackendCachePolicies.MentorScopeReads);

        return new ServiceResult<object>(ResultType.Success);
    }
}
