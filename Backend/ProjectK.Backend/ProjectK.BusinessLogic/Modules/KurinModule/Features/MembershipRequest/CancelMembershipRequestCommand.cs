using MediatR;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;

/// <summary>The asking kurin withdraws a request nobody has answered yet.</summary>
public sealed record CancelMembershipRequestCommand(Guid KurinKey, Guid MembershipRequestKey) : IRequest<ServiceResult<Unit>>;

public sealed class CancelMembershipRequestCommandHandler : IRequestHandler<CancelMembershipRequestCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public CancelMembershipRequestCommandHandler(IUnitOfWork unitOfWork, ICurrentUserContext currentUser, TimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Unit>> Handle(CancelMembershipRequestCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != request.KurinKey)
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, KurinErrors.MembershipRequestOtherKurin, "A kurin reads and answers only its own requests.");
        }

        var asked = await _unitOfWork.MembershipRequests.GetAsync(request.MembershipRequestKey, cancellationToken);
        if (asked is null || asked.ToKurinKey != request.KurinKey)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, KurinErrors.RequestNotFound, "No such request from this kurin.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        if (!asked.IsOpen(now))
        {
            return ServiceResult<Unit>.Failure(ResultType.Conflict, KurinErrors.RequestClosed, "The request was already answered, withdrawn or has lapsed.");
        }

        asked.Close(MembershipRequestStatus.Cancelled, now, _currentUser.UserId);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (StaleRowException)
        {
            return ServiceResult<Unit>.Failure(ResultType.Conflict, KurinErrors.RequestClosed, "The request was already answered, withdrawn or has lapsed.");
        }

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
