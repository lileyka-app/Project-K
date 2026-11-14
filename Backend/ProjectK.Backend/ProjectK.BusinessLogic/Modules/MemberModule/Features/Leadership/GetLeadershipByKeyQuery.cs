using MediatR;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Leadership;

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
            return ServiceResult<LeadershipDto>.Failure(ResultType.NotFound, LeadershipErrors.LeadershipNotFound, "No such leadership.");
        }
        var response = leadership.ToDto(_links);
        return new ServiceResult<LeadershipDto>(ResultType.Success, response);
    }
}
