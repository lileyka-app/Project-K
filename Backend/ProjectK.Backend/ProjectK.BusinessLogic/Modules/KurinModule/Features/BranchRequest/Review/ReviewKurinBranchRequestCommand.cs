using MediatR;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest.Review;

/// <summary>
/// An administrator settles a kurin's request to change its гілки. Approving gives the kurin exactly
/// the гілки it asked for, provided each number is still free in its гілка of the край.
/// </summary>
public sealed record ReviewKurinBranchRequestCommand(Guid KurinBranchRequestKey, bool Approve) : IRequest<ServiceResult<bool>>;

public sealed class ReviewKurinBranchRequestCommandHandler : IRequestHandler<ReviewKurinBranchRequestCommand, ServiceResult<bool>>
{
    /// <summary>What the kurin's journal calls the change.</summary>
    public const string Label = "Гілки куреня";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly IKurinAudit _audit;
    private readonly IDomainEventPublisher _events;
    private readonly IBackendCache _cache;

    public ReviewKurinBranchRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        IKurinAudit audit,
        IDomainEventPublisher events,
        IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _audit = audit;
        _events = events;
        _cache = cache;
    }

    public async Task<ServiceResult<bool>> Handle(ReviewKurinBranchRequestCommand request, CancellationToken cancellationToken)
    {
        var pending = await _unitOfWork.KurinBranchRequests.GetByKeyAsync(request.KurinBranchRequestKey, cancellationToken);
        if (pending is null || pending.Status != KurinBranchRequestStatus.Pending)
        {
            return ServiceResult<bool>.Failure(ResultType.NotFound, "KurinBranchRequestNotFound", "There is no such request waiting.");
        }

        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(pending.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return ServiceResult<bool>.Failure(ResultType.NotFound, "KurinNotFound", "Kurin not found.");
        }

        // The settlement and the change it makes commit together; disposing the transaction without a
        // commit takes both back.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var status = request.Approve ? KurinBranchRequestStatus.Approved : KurinBranchRequestStatus.Rejected;

        if (request.Approve)
        {
            foreach (var numbering in pending.WantedBranches)
            {
                if (await _unitOfWork.Kurins.IsNumberTakenAsync(kurin.KraiCode, numbering.Branch, numbering.Number!.Value, kurin.KurinKey, cancellationToken))
                {
                    return ServiceResult<bool>.Failure(ResultType.Conflict, "KurinNumberTaken", "A kurin with this number already exists in this край.");
                }
            }

            var before = KurinBranchNames.DescribeSenior(kurin.Numbering);
            kurin.SetBranches(pending.WantedBranches);
            _unitOfWork.Kurins.Update(kurin, cancellationToken);
            await _audit.RecordAsync(
                new AuditRecord(
                    AuditArea.Settings,
                    AuditActions.Settings.KurinChanged,
                    kurin.KurinKey,
                    Label,
                    Changes: AuditChanges.Of(("branches", before, KurinBranchNames.DescribeSenior(kurin.Numbering))),
                    KurinKey: kurin.KurinKey),
                cancellationToken);
        }

        if (!await _unitOfWork.KurinBranchRequests.TrySettleAsync(pending.KurinBranchRequestKey, status, _currentUser.UserId, DateTime.UtcNow, cancellationToken))
        {
            return ServiceResult<bool>.Failure(ResultType.Conflict, "KurinBranchRequestAlreadySettled", "Someone settled this request a moment ago.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        pending.Status = status;

        if (request.Approve)
        {
            _cache.Invalidate(BackendCachePolicies.KurinReads);
        }

        await _events.PublishAsync(
            new KurinBranchRequestReviewed(
                pending.KurinBranchRequestKey,
                kurin.KurinKey,
                request.Approve,
                KurinBranchNames.DescribeSenior(request.Approve ? kurin.Numbering : pending.WantedBranches),
                pending.RequestedByUserKey,
                _currentUser.UserId),
            cancellationToken);
        return new ServiceResult<bool>(ResultType.Success, true);
    }

}
