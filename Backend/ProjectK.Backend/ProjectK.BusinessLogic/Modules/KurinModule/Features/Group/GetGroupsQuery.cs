using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;

public class GetGroupsQuery : IRequest<ServiceResult<IEnumerable<GroupDto>>>
{
    public Guid KurinKey { get; set; }
    public GetGroupsQuery(Guid kurinKey)
    {
        KurinKey = kurinKey;
    }
}

public class GetGroupsQueryHandler : IRequestHandler<GetGroupsQuery, ServiceResult<IEnumerable<GroupDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;
    private readonly IBackendCache _cache;

    public GetGroupsQueryHandler(IUnitOfWork unitOfWork, IBlobReadLinks links, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _links = links;
        _cache = cache;
    }

    public async Task<ServiceResult<IEnumerable<GroupDto>>> Handle(GetGroupsQuery request, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            BackendCacheRules.GroupReads,
            $"list:kurin:{request.KurinKey}",
            async token =>
            {
                var groups = await _unitOfWork.Groups.GetAllAsync(request.KurinKey, token);
                var response = groups.Select(item => item.ToDto(_links)).ToList();
                return new ServiceResult<IEnumerable<GroupDto>>(ResultType.Success, response);
            },
            cancellationToken);
    }
}
