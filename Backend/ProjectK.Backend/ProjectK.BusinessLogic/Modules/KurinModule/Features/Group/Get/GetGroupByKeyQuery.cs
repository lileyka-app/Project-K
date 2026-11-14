using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Get;

public class GetGroupByKeyQuery : IRequest<ServiceResult<GroupResponse>>
{
    public Guid GroupKey { get; set; }
    public GetGroupByKeyQuery(Guid groupKey)
    {
        GroupKey = groupKey;
    }
}

public class GetGroupByKeyQueryHandler : IRequestHandler<GetGroupByKeyQuery, ServiceResult<GroupResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IBackendCache _cache;
    public GetGroupByKeyQueryHandler(IUnitOfWork unitOfWork, IMapper mapper, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<ServiceResult<GroupResponse>> Handle(GetGroupByKeyQuery request, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            BackendCachePolicies.GroupReads,
            $"by-key:{request.GroupKey}",
            async token =>
            {
                var group = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey, token);

                if (group is null)
                {
                    return new ServiceResult<GroupResponse>(ResultType.NotFound);
                }

                var response = _mapper.Map<GroupResponse>(group);

                return new ServiceResult<GroupResponse>(ResultType.Success, response);
            },
            cancellationToken);
    }
}
