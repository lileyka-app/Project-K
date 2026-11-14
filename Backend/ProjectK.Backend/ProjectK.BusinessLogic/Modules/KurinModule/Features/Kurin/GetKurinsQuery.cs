using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

public class GetKurinsQuery : IRequest<ServiceResult<IEnumerable<KurinDto>>>
{
}

public class GetKurinsQueryHandler : IRequestHandler<GetKurinsQuery, ServiceResult<IEnumerable<KurinDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;
    private readonly IBackendCache _cache;

    public GetKurinsQueryHandler(IUnitOfWork unitOfWork, IBlobReadLinks links, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _links = links;
        _cache = cache;
    }

    public async Task<ServiceResult<IEnumerable<KurinDto>>> Handle(GetKurinsQuery request, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            BackendCachePolicies.KurinReads,
            "list",
            async token =>
            {
                var kurins = await _unitOfWork.Kurins.GetAllAsync(token);
                var kurinResponses = kurins.Select(item => item.ToDto(_links)).ToList();
                return new ServiceResult<IEnumerable<KurinDto>>(ResultType.Success, kurinResponses);
            },
            cancellationToken);
    }
}
