using MediatR;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;

public sealed class GetLeadershipByTypeQuery : IRequest<ServiceResult<LeadershipDto>>
{
    public LeadershipType LeadershipType { get; }
    public Guid TypeKey { get; }
    public GetLeadershipByTypeQuery(string leadershipType, Guid typeKey)
    {
        LeadershipType = Enum.Parse<LeadershipType>(leadershipType, true);
        TypeKey = typeKey;
    }
}

public sealed class GetLeadershipByTypeQueryHandler : IRequestHandler<GetLeadershipByTypeQuery, ServiceResult<LeadershipDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;

    public GetLeadershipByTypeQueryHandler(IUnitOfWork unitOfWork, IMemberDirectory members)
    {
        _unitOfWork = unitOfWork;
        _members = members;
    }

    public async Task<ServiceResult<LeadershipDto>> Handle(GetLeadershipByTypeQuery request, CancellationToken cancellationToken)
    {
        var leadership = await _unitOfWork.Leaderships.GetAllByTypeAsync(request.LeadershipType, request.TypeKey, cancellationToken);
        var currentLeadership = leadership.Where(l => l.EndDate == null);
        if (leadership.Count() > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Multiple leaderships found for the given type and key.");
        }
        var current = currentLeadership.FirstOrDefault();
        var leadershipResponse = current?.ToDto(await _members.GetLookupsAsync(current.HolderKeys(), cancellationToken));
        return new ServiceResult<LeadershipDto>(
            ResultType.Success,
            leadershipResponse);
    }
}
