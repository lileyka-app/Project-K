using MediatR;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KraiModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;

/// <summary>The card fields a kurin may hide, for its settings page.</summary>
public sealed record GetKurinCardFieldsQuery(Guid KurinKey) : IRequest<ServiceResult<IReadOnlyList<KurinCardFieldDto>>>;

public sealed class GetKurinCardFieldsQueryHandler : IRequestHandler<GetKurinCardFieldsQuery, ServiceResult<IReadOnlyList<KurinCardFieldDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKraiProfileDirectory _profiles;

    public GetKurinCardFieldsQueryHandler(IUnitOfWork unitOfWork, IKraiProfileDirectory profiles)
    {
        _unitOfWork = unitOfWork;
        _profiles = profiles;
    }

    public async Task<ServiceResult<IReadOnlyList<KurinCardFieldDto>>> Handle(GetKurinCardFieldsQuery request, CancellationToken cancellationToken)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        var profile = kurin is null ? null : await _profiles.GetForKraiAsync(kurin.KraiCode, cancellationToken);
        return kurin is null || profile is null
            ? ServiceResult<IReadOnlyList<KurinCardFieldDto>>.Failure(ResultType.NotFound, KraiErrors.KurinNotFound, "Kurin not found.")
            : new ServiceResult<IReadOnlyList<KurinCardFieldDto>>(ResultType.Success, profile.CardFields.ToCardFieldDtos(kurin.HiddenCardFields));
    }
}
