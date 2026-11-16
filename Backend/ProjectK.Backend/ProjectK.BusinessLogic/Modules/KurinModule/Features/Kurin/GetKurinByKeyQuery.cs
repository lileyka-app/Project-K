using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

public class GetKurinByKeyQuery : IRequest<ServiceResult<KurinDto>>
{
    public Guid KurinKey { get; set; }

    public GetKurinByKeyQuery(Guid kurinKey)
    {
        KurinKey = kurinKey;
    }
}

public class GetKurinByKeyQueryHandler : IRequestHandler<GetKurinByKeyQuery, ServiceResult<KurinDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;
    private readonly IBackendCache _cache;
    private readonly IMembershipDirectory _memberships;

    public GetKurinByKeyQueryHandler(IUnitOfWork unitOfWork, IBlobReadLinks links, IBackendCache cache, IMembershipDirectory memberships)
    {
        _unitOfWork = unitOfWork;
        _links = links;
        _cache = cache;
        _memberships = memberships;
    }

    public async Task<ServiceResult<KurinDto>> Handle(GetKurinByKeyQuery request, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            BackendCacheRules.KurinReads,
            $"by-key:{request.KurinKey}",
            async token =>
            {
                var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, token);

                if (kurin is null)
                {
                    return KurinErrors.NotFound<KurinDto>();
                }

                var headcount = await _memberships.CountCurrentInKurinAsync(request.KurinKey, token);
                var kurinResponse = kurin.ToDto(_links, headcount);

                return new ServiceResult<KurinDto>(ResultType.Success, kurinResponse);
            },
            cancellationToken);
    }
}
