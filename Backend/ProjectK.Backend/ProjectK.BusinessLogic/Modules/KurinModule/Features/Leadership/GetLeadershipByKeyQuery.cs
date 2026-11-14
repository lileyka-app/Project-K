using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;

public class GetLeadershipByKeyQuery : IRequest<ServiceResult<LeadershipResponse>>
{
    public Guid LeadershipKey { get; }
    public GetLeadershipByKeyQuery(Guid leadershipKey)
    {
        LeadershipKey = leadershipKey;
    }
}

public class GetLeadershipByKeyQueryHandler : IRequestHandler<GetLeadershipByKeyQuery, ServiceResult<LeadershipResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    public GetLeadershipByKeyQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ServiceResult<LeadershipResponse>> Handle(GetLeadershipByKeyQuery request, CancellationToken cancellationToken)
    {
        var leadership = await _unitOfWork.Leaderships.GetByKeyAsync(request.LeadershipKey, cancellationToken);
        if (leadership == null)
        {
            return new ServiceResult<LeadershipResponse>(ResultType.NotFound);
        }
        var response = _mapper.Map<LeadershipResponse>(leadership);
        return new ServiceResult<LeadershipResponse>(ResultType.Success, response);
    }
}
