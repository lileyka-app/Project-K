using MediatR;
using ProjectK.Common.Exceptions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest.Cancel;

/// <summary>The asking kurin withdraws a request nobody has answered yet.</summary>
public sealed record CancelMembershipRequestCommand(Guid KurinKey, Guid MembershipRequestKey) : IRequest<ServiceResult<object>>;

public sealed class CancelMembershipRequestCommandHandler : IRequestHandler<CancelMembershipRequestCommand, ServiceResult<object>>
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

    public async Task<ServiceResult<object>> Handle(CancelMembershipRequestCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != request.KurinKey)
        {
            return new ServiceResult<object>(ResultType.Forbidden);
        }

        var asked = await _unitOfWork.MembershipRequests.GetAsync(request.MembershipRequestKey, cancellationToken);
        if (asked is null || asked.ToKurinKey != request.KurinKey)
        {
            return ServiceResult<object>.Failure(ResultType.NotFound, "RequestNotFound", "No such request from this kurin.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        if (!asked.IsOpen(now))
        {
            return ServiceResult<object>.Failure(ResultType.Conflict, "RequestClosed", "The request was already answered, withdrawn or has lapsed.");
        }

        asked.Close(MembershipRequestStatus.Cancelled, now, _currentUser.UserId);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (StaleRowException)
        {
            return ServiceResult<object>.Failure(ResultType.Conflict, "RequestClosed", "The request was already answered, withdrawn or has lapsed.");
        }

        return new ServiceResult<object>(ResultType.Success);
    }
}
