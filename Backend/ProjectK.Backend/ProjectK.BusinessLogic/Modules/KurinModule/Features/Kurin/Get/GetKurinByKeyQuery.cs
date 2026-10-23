using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Get;

public class GetKurinByKeyQuery : IRequest<ServiceResult<KurinResponse>>
{
    public Guid KurinKey { get; set; }

    public GetKurinByKeyQuery(Guid kurinKey)
    {
        KurinKey = kurinKey;
    }
}

public class GetKurinByKeyQueryHandler : IRequestHandler<GetKurinByKeyQuery, ServiceResult<KurinResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IBackendCache _cache;

    public GetKurinByKeyQueryHandler(IUnitOfWork unitOfWork, IMapper mapper, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<ServiceResult<KurinResponse>> Handle(GetKurinByKeyQuery request, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            BackendCachePolicies.KurinReads,
            $"by-key:{request.KurinKey}",
            async token =>
            {
                var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, token);

                if (kurin is null)
                {
                    return new ServiceResult<KurinResponse>(ResultType.NotFound);
                }

                var accountsHere = await _unitOfWork.Memberships
                    .GetAccountKeysInKurinAsync(request.KurinKey, token);
                var activeUsersCount = await _unitOfWork.Users
                    .CountActiveAsync(accountsHere, token);

                var kurinResponse = _mapper.Map<KurinResponse>(kurin);
                kurinResponse.CurrentUserCount = activeUsersCount;

                return new ServiceResult<KurinResponse>(ResultType.Success, kurinResponse);
            },
            cancellationToken);
    }
}
