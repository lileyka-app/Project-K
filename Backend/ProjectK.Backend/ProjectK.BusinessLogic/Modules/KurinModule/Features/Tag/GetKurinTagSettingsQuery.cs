using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.KurinModule.Tags;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Tag;

/// <summary>The kurin's tag switches for its settings page. Gated as kurin settings.</summary>
public sealed record GetKurinTagSettingsQuery(Guid KurinKey) : IRequest<ServiceResult<KurinTagSettingsDto>>;

public sealed class GetKurinTagSettingsQueryHandler : IRequestHandler<GetKurinTagSettingsQuery, ServiceResult<KurinTagSettingsDto>>
{
    private readonly IKurinTagVisibility _visibility;

    public GetKurinTagSettingsQueryHandler(IKurinTagVisibility visibility)
    {
        _visibility = visibility;
    }

    public async Task<ServiceResult<KurinTagSettingsDto>> Handle(GetKurinTagSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _visibility.SettingsAsync(request.KurinKey, cancellationToken);
        return settings is null
            ? KurinErrors.NotFound<KurinTagSettingsDto>()
            : new ServiceResult<KurinTagSettingsDto>(ResultType.Success,
                new KurinTagSettingsDto(settings.Enabled, settings.EnabledByDefault, settings.OwnTagsVisible));
    }
}
