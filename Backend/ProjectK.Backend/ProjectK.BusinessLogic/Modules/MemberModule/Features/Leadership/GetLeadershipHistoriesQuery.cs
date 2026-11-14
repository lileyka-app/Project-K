using MediatR;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Leadership;

public class GetLeadershipHistoriesQuery : IRequest<ServiceResult<IEnumerable<LeadershipHistoryMemberDto>>>
{
    public Guid LeadershipKey { get; }
    public GetLeadershipHistoriesQuery(Guid leadershipKey)
    {
        LeadershipKey = leadershipKey;
    }
}

public class GetLeadershipHistoriesQueryHandler : IRequestHandler<GetLeadershipHistoriesQuery, ServiceResult<IEnumerable<LeadershipHistoryMemberDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;
    public GetLeadershipHistoriesQueryHandler(IUnitOfWork unitOfWork, IBlobReadLinks links)
    {
        _unitOfWork = unitOfWork;
        _links = links;
    }
    public async Task<ServiceResult<IEnumerable<LeadershipHistoryMemberDto>>> Handle(GetLeadershipHistoriesQuery request, CancellationToken cancellationToken)
    {
        var histories = await _unitOfWork.Leaderships.GetLeadershipHistoriesAsync(request.LeadershipKey, cancellationToken);
        var historyDtos = histories.Select(history => history.ToHolderDto(_links)).ToList();
        return new ServiceResult<IEnumerable<LeadershipHistoryMemberDto>>(ResultType.Success, historyDtos);
    }
}
