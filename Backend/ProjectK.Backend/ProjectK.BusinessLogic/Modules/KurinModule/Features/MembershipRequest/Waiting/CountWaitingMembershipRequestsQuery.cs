using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest.Waiting;

/// <summary>
/// How many requests from other kurins still wait for this kurin's answer: the menu's dot. A count and
/// nothing else, since the menu asks on every page.
/// </summary>
public sealed record CountWaitingMembershipRequestsQuery(Guid KurinKey) : IRequest<ServiceResult<int>>;

public sealed class CountWaitingMembershipRequestsQueryHandler
    : IRequestHandler<CountWaitingMembershipRequestsQuery, ServiceResult<int>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public CountWaitingMembershipRequestsQueryHandler(IUnitOfWork unitOfWork, ICurrentUserContext currentUser, TimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<int>> Handle(CountWaitingMembershipRequestsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != request.KurinKey)
        {
            return new ServiceResult<int>(ResultType.Forbidden);
        }

        var waiting = await _unitOfWork.MembershipRequests.CountOpenToAnswerAsync(
            request.KurinKey, _time.GetUtcNow().UtcDateTime, cancellationToken);
        return new ServiceResult<int>(ResultType.Success, waiting);
    }
}
