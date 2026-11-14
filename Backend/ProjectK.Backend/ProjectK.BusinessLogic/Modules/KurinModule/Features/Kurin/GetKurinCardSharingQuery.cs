using System.Text.Json.Serialization;
using MediatR;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

public sealed record GetKurinCardSharingQuery(Guid KurinKey) : IRequest<ServiceResult<KurinCardSharingDto>>;

public sealed class GetKurinCardSharingQueryHandler : IRequestHandler<GetKurinCardSharingQuery, ServiceResult<KurinCardSharingDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetKurinCardSharingQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<KurinCardSharingDto>> Handle(GetKurinCardSharingQuery request, CancellationToken cancellationToken)
    {
        var settings = await _unitOfWork.KurinCardSharing.GetForKurinAsync(request.KurinKey, cancellationToken);
        return new ServiceResult<KurinCardSharingDto>(ResultType.Success, settings.ToDto());
    }
}
