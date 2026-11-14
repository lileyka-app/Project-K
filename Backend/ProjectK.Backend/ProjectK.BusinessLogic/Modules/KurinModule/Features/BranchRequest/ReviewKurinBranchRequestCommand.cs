using MediatR;
using ProjectK.BusinessLogic.Modules.MemberModule;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest;

/// <summary>
/// An administrator settles a kurin's request to change its гілки. Approving gives the kurin exactly
/// the гілки it asked for, provided each number is still free in its гілка of the край.
/// </summary>
public sealed record ReviewKurinBranchRequestCommand(Guid KurinBranchRequestKey, bool Approve) : IRequest<ServiceResult<Unit>>;

public sealed class ReviewKurinBranchRequestCommandHandler : IRequestHandler<ReviewKurinBranchRequestCommand, ServiceResult<Unit>>
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

    public async Task<ServiceResult<Unit>> Handle(ReviewKurinBranchRequestCommand request, CancellationToken cancellationToken)
    {
        var pending = await _unitOfWork.KurinBranchRequests.GetByKeyAsync(request.KurinBranchRequestKey, cancellationToken);
        if (pending is null || pending.Status != KurinBranchRequestStatus.Pending)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, MemberErrors.KurinBranchRequestNotFound, "There is no such request waiting.");
        }

        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(pending.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, MemberErrors.KurinNotFound, "Kurin not found.");
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
                    return ServiceResult<Unit>.Failure(ResultType.Conflict, MemberErrors.KurinNumberTaken, "A kurin with this number already exists in this край.");
                }
            }

            var before = KurinBranchNames.DescribeSenior(kurin.Numbering);
            kurin.SetBranches(pending.WantedBranches);
            _unitOfWork.Kurins.Update(kurin, cancellationToken);
            await _audit.RecordAsync(
                new AuditRecord(
                    AuditArea.Settings,
                    AuditActionNames.Settings.KurinChanged,
                    kurin.KurinKey,
                    Label,
                    Changes: AuditChangeRules.Of(("branches", before, KurinBranchNames.DescribeSenior(kurin.Numbering))),
                    KurinKey: kurin.KurinKey),
                cancellationToken);
        }

        if (!await _unitOfWork.KurinBranchRequests.TrySettleAsync(pending.KurinBranchRequestKey, status, _currentUser.UserId, DateTime.UtcNow, cancellationToken))
        {
            return ServiceResult<Unit>.Failure(ResultType.Conflict, MemberErrors.KurinBranchRequestAlreadySettled, "Someone settled this request a moment ago.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        pending.Status = status;

        if (request.Approve)
        {
            _cache.Invalidate(BackendCacheRules.KurinReads);
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
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }

}
