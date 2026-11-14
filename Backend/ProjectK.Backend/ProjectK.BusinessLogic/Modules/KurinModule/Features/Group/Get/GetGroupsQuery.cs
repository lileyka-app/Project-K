using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Get;

public class GetGroupsQuery : IRequest<ServiceResult<IEnumerable<GroupResponse>>>
{
    public Guid KurinKey { get; set; }
    public GetGroupsQuery(Guid kurinKey)
    {
        KurinKey = kurinKey;
    }
}

public class GetGroupsQueryHandler : IRequestHandler<GetGroupsQuery, ServiceResult<IEnumerable<GroupResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IBackendCache _cache;

    public GetGroupsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<ServiceResult<IEnumerable<GroupResponse>>> Handle(GetGroupsQuery request, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            BackendCachePolicies.GroupReads,
            $"list:kurin:{request.KurinKey}",
            async token =>
            {
                var groups = await _unitOfWork.Groups.GetAllAsync(request.KurinKey, token);
                var response = _mapper.Map<IEnumerable<GroupResponse>>(groups).ToList();
                return new ServiceResult<IEnumerable<GroupResponse>>(ResultType.Success, response);
            },
            cancellationToken);
    }
}
