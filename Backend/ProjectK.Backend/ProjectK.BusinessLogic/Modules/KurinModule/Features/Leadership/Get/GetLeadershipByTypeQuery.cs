using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership.Get;

public class GetLeadershipByTypeQuery : IRequest<ServiceResult<LeadershipResponse>>
{
    public LeadershipType LeadershipType { get; }
    public Guid TypeKey { get; }
    public GetLeadershipByTypeQuery(string leadershipType, Guid typeKey)
    {
        LeadershipType = Enum.Parse<LeadershipType>(leadershipType, true);
        TypeKey = typeKey;
    }
}

public class GetLeadershipByTypeQueryHandler : IRequestHandler<GetLeadershipByTypeQuery, ServiceResult<LeadershipResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetLeadershipByTypeQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ServiceResult<LeadershipResponse>> Handle(GetLeadershipByTypeQuery request, CancellationToken cancellationToken)
    {
        var leadership = await _unitOfWork.Leaderships.GetAllByTypeAsync(request.LeadershipType, request.TypeKey, cancellationToken);
        var currentLeadership = leadership.Where(l => l.EndDate == null);
        if (leadership.Count() > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Multiple leaderships found for the given type and key.");
        }
        LeadershipResponse leadershipResponse = _mapper.Map<LeadershipResponse>(currentLeadership.FirstOrDefault());
        return new ServiceResult<LeadershipResponse>(
            ResultType.Success,
            leadershipResponse);
    }
}
