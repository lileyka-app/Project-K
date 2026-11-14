using MediatR;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;

public class GetLeadershipByTypeQuery : IRequest<ServiceResult<LeadershipDto>>
{
    public LeadershipType LeadershipType { get; }
    public Guid TypeKey { get; }
    public GetLeadershipByTypeQuery(string leadershipType, Guid typeKey)
    {
        LeadershipType = Enum.Parse<LeadershipType>(leadershipType, true);
        TypeKey = typeKey;
    }
}

public class GetLeadershipByTypeQueryHandler : IRequestHandler<GetLeadershipByTypeQuery, ServiceResult<LeadershipDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;

    public GetLeadershipByTypeQueryHandler(IUnitOfWork unitOfWork, IBlobReadLinks links)
    {
        _unitOfWork = unitOfWork;
        _links = links;
    }

    public async Task<ServiceResult<LeadershipDto>> Handle(GetLeadershipByTypeQuery request, CancellationToken cancellationToken)
    {
        var leadership = await _unitOfWork.Leaderships.GetAllByTypeAsync(request.LeadershipType, request.TypeKey, cancellationToken);
        var currentLeadership = leadership.Where(l => l.EndDate == null);
        if (leadership.Count() > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Multiple leaderships found for the given type and key.");
        }
        var leadershipResponse = currentLeadership.FirstOrDefault()?.ToDto(_links);
        return new ServiceResult<LeadershipDto>(
            ResultType.Success,
            leadershipResponse);
    }
}
