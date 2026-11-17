using MediatR;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;

public sealed class GetGroupByKeyQuery : IRequest<ServiceResult<GroupDto>>
{
    public Guid GroupKey { get; set; }
    public GetGroupByKeyQuery(Guid groupKey)
    {
        GroupKey = groupKey;
    }
}

public sealed class GetGroupByKeyQueryHandler : IRequestHandler<GetGroupByKeyQuery, ServiceResult<GroupDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;
    private readonly IBackendCache _cache;
    public GetGroupByKeyQueryHandler(IUnitOfWork unitOfWork, IBlobReadLinks links, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _links = links;
        _cache = cache;
    }

    public async Task<ServiceResult<GroupDto>> Handle(GetGroupByKeyQuery request, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            BackendCacheRules.GroupReads,
            $"by-key:{request.GroupKey}",
            async token =>
            {
                var group = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey, token);

                if (group is null)
                {
                    return KurinErrors.GroupMissing<GroupDto>();
                }

                var response = group.ToDto(_links);

                return new ServiceResult<GroupDto>(ResultType.Success, response);
            },
            cancellationToken);
    }
}
