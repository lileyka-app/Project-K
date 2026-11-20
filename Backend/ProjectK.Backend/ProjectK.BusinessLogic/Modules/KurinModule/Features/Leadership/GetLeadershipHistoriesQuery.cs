using MediatR;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;

public sealed class GetLeadershipHistoriesQuery : IRequest<ServiceResult<IEnumerable<LeadershipHistoryMemberDto>>>
{
    public Guid LeadershipKey { get; }
    public GetLeadershipHistoriesQuery(Guid leadershipKey)
    {
        LeadershipKey = leadershipKey;
    }
}

public sealed class GetLeadershipHistoriesQueryHandler : IRequestHandler<GetLeadershipHistoriesQuery, ServiceResult<IEnumerable<LeadershipHistoryMemberDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    public GetLeadershipHistoriesQueryHandler(IUnitOfWork unitOfWork, IMemberDirectory members)
    {
        _unitOfWork = unitOfWork;
        _members = members;
    }
    public async Task<ServiceResult<IEnumerable<LeadershipHistoryMemberDto>>> Handle(GetLeadershipHistoriesQuery request, CancellationToken cancellationToken)
    {
        var histories = await _unitOfWork.Leaderships.GetLeadershipHistoriesAsync(request.LeadershipKey, cancellationToken);
        var people = await _members.GetLookupsAsync(histories.Select(history => history.MemberKey).Distinct().ToArray(), cancellationToken);
        var historyDtos = histories.Select(history => history.ToHolderDto(people)).ToList();
        return new ServiceResult<IEnumerable<LeadershipHistoryMemberDto>>(ResultType.Success, historyDtos);
    }
}
