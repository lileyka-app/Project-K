using MediatR;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;

public class GetLeadershipByKeyQuery : IRequest<ServiceResult<LeadershipDto>>
{
    public Guid LeadershipKey { get; }
    public GetLeadershipByKeyQuery(Guid leadershipKey)
    {
        LeadershipKey = leadershipKey;
    }
}

public class GetLeadershipByKeyQueryHandler : IRequestHandler<GetLeadershipByKeyQuery, ServiceResult<LeadershipDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;
    public GetLeadershipByKeyQueryHandler(IUnitOfWork unitOfWork, IBlobReadLinks links)
    {
        _unitOfWork = unitOfWork;
        _links = links;
    }

    public async Task<ServiceResult<LeadershipDto>> Handle(GetLeadershipByKeyQuery request, CancellationToken cancellationToken)
    {
        var leadership = await _unitOfWork.Leaderships.GetByKeyAsync(request.LeadershipKey, cancellationToken);
        if (leadership == null)
        {
            return new ServiceResult<LeadershipDto>(ResultType.NotFound);
        }
        var response = leadership.ToDto(_links);
        return new ServiceResult<LeadershipDto>(ResultType.Success, response);
    }
}
