using MediatR;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

public sealed class GetKurinsQuery : IRequest<ServiceResult<IEnumerable<KurinDto>>>
{
}

public sealed class GetKurinsQueryHandler : IRequestHandler<GetKurinsQuery, ServiceResult<IEnumerable<KurinDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;
    private readonly IBackendCache _cache;
    private readonly IMembershipDirectory _memberships;

    public GetKurinsQueryHandler(IUnitOfWork unitOfWork, IBlobReadLinks links, IBackendCache cache, IMembershipDirectory memberships)
    {
        _unitOfWork = unitOfWork;
        _links = links;
        _cache = cache;
        _memberships = memberships;
    }

    public async Task<ServiceResult<IEnumerable<KurinDto>>> Handle(GetKurinsQuery request, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            BackendCacheRules.KurinReads,
            "list",
            async token =>
            {
                var kurins = await _unitOfWork.Kurins.GetAllAsync(token);
                var headcounts = await _memberships.CountCurrentInKurinsAsync(kurins.Select(item => item.KurinKey).ToList(), token);
                var kurinResponses = kurins
                    .Select(item => item.ToDto(_links, headcounts.GetValueOrDefault(item.KurinKey)))
                    .ToList();
                return new ServiceResult<IEnumerable<KurinDto>>(ResultType.Success, kurinResponses);
            },
            cancellationToken);
    }
}
