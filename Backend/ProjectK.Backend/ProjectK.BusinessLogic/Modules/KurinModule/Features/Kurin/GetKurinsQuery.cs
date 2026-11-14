using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

public class GetKurinsQuery : IRequest<ServiceResult<IEnumerable<KurinResponse>>>
{
}

public class GetKurinsQueryHandler : IRequestHandler<GetKurinsQuery, ServiceResult<IEnumerable<KurinResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IBackendCache _cache;

    public GetKurinsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<ServiceResult<IEnumerable<KurinResponse>>> Handle(GetKurinsQuery request, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            BackendCachePolicies.KurinReads,
            "list",
            async token =>
            {
                var kurins = await _unitOfWork.Kurins.GetAllAsync(token);
                var kurinResponses = _mapper.Map<IEnumerable<KurinResponse>>(kurins).ToList();
                return new ServiceResult<IEnumerable<KurinResponse>>(ResultType.Success, kurinResponses);
            },
            cancellationToken);
    }
}
