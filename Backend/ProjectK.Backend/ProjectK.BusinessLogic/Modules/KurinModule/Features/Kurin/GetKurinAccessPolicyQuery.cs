using MediatR;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

/// <summary>The kurin's «Права й видимість», every cell with its default and lock.</summary>
public sealed record GetKurinAccessPolicyQuery(Guid KurinKey) : IRequest<ServiceResult<KurinAccessPolicyDto>>;

public sealed class GetKurinAccessPolicyQueryHandler : IRequestHandler<GetKurinAccessPolicyQuery, ServiceResult<KurinAccessPolicyDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKurinAccessPolicyProvider _policies;

    public GetKurinAccessPolicyQueryHandler(IUnitOfWork unitOfWork, IKurinAccessPolicyProvider policies)
    {
        _unitOfWork = unitOfWork;
        _policies = policies;
    }

    public async Task<ServiceResult<KurinAccessPolicyDto>> Handle(GetKurinAccessPolicyQuery request, CancellationToken cancellationToken)
    {
        if (await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken) is null)
        {
            return KurinErrors.NotFound<KurinAccessPolicyDto>();
        }

        var policy = await _policies.GetAsync(request.KurinKey, cancellationToken);
        return new ServiceResult<KurinAccessPolicyDto>(ResultType.Success, policy.ToDto());
    }
}
