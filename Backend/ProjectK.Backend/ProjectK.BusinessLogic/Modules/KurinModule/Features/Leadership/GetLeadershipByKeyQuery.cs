using MediatR;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;

public sealed class GetLeadershipByKeyQuery : IRequest<ServiceResult<LeadershipDto>>
{
    public Guid LeadershipKey { get; }
    public GetLeadershipByKeyQuery(Guid leadershipKey)
    {
        LeadershipKey = leadershipKey;
    }
}

public sealed class GetLeadershipByKeyQueryHandler : IRequestHandler<GetLeadershipByKeyQuery, ServiceResult<LeadershipDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    public GetLeadershipByKeyQueryHandler(IUnitOfWork unitOfWork, IMemberDirectory members)
    {
        _unitOfWork = unitOfWork;
        _members = members;
    }

    public async Task<ServiceResult<LeadershipDto>> Handle(GetLeadershipByKeyQuery request, CancellationToken cancellationToken)
    {
        var leadership = await _unitOfWork.Leaderships.GetByKeyAsync(request.LeadershipKey, cancellationToken);
        if (leadership == null)
        {
            return ServiceResult<LeadershipDto>.Failure(ResultType.NotFound, KurinErrors.LeadershipNotFound, "No such leadership.");
        }
        var response = leadership.ToDto(await _members.GetLookupsAsync(leadership.HolderKeys(), cancellationToken));
        return new ServiceResult<LeadershipDto>(ResultType.Success, response);
    }
}
