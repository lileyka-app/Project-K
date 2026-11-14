using AutoMapper;
using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership.Get;

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
    private readonly IMapper _mapper;
    public GetLeadershipHistoriesQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }
    public async Task<ServiceResult<IEnumerable<LeadershipHistoryMemberDto>>> Handle(GetLeadershipHistoriesQuery request, CancellationToken cancellationToken)
    {
        var histories = await _unitOfWork.Leaderships.GetLeadershipHistoriesAsync(request.LeadershipKey, cancellationToken);
        var historyDtos = _mapper.Map<IEnumerable<LeadershipHistoryMemberDto>>(histories);
        return new ServiceResult<IEnumerable<LeadershipHistoryMemberDto>>(ResultType.Success, historyDtos);
    }
}
