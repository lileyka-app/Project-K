using MediatR;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.KraiModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;

/// <summary>The profile a kurin works under, for the web and the PWA to read once per kurin.</summary>
public sealed record GetKurinProfileQuery(Guid KurinKey) : IRequest<ServiceResult<KraiProfile>>;

public sealed class GetKurinProfileQueryHandler : IRequestHandler<GetKurinProfileQuery, ServiceResult<KraiProfile>>
{
    private readonly IKraiProfileProvider _profiles;

    public GetKurinProfileQueryHandler(IKraiProfileProvider profiles)
    {
        _profiles = profiles;
    }

    public async Task<ServiceResult<KraiProfile>> Handle(GetKurinProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetForKurinAsync(request.KurinKey, cancellationToken);
        return profile is null
            ? ServiceResult<KraiProfile>.Failure(ResultType.NotFound, KraiErrors.KurinNotFound, "Kurin not found.")
            : new ServiceResult<KraiProfile>(ResultType.Success, profile);
    }
}
