using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Tag;

/// <summary>
/// The kurin's tags for anyone in it: names and colours, so the people's tags can be drawn. How many
/// carry each tag only for whoever manages them — to others the counts would tell more than they see.
/// </summary>
public sealed record GetKurinTagsQuery(Guid KurinKey) : IRequest<ServiceResult<KurinTagsDto>>;

public sealed class GetKurinTagsQueryHandler : IRequestHandler<GetKurinTagsQuery, ServiceResult<KurinTagsDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IKurinTagVisibility _visibility;

    public GetKurinTagsQueryHandler(IUnitOfWork uow, IKurinTagVisibility visibility)
    {
        _uow = uow;
        _visibility = visibility;
    }

    public async Task<ServiceResult<KurinTagsDto>> Handle(GetKurinTagsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _visibility.SettingsAsync(request.KurinKey, cancellationToken);
        if (settings is null)
        {
            return KurinErrors.NotFound<KurinTagsDto>();
        }

        if (!settings.Enabled)
        {
            return new ServiceResult<KurinTagsDto>(ResultType.Success,
                new KurinTagsDto(false, false, KurinTagRules.MaxTagsPerMember, KurinTagRules.Palette, []));
        }

        var canManage = await _visibility.CanManageAsync(request.KurinKey, cancellationToken);
        var tags = await _uow.KurinTags.GetForKurinAsync(request.KurinKey, cancellationToken);

        IReadOnlyDictionary<Guid, int> counts = new Dictionary<Guid, int>();
        if (canManage)
        {
            counts = (await _uow.KurinTags.GetAssignmentsAsync(request.KurinKey, cancellationToken))
                .GroupBy(a => a.KurinTagKey)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        var items = tags
            .Select(tag => new KurinTagDto(
                tag.KurinTagKey,
                tag.Name,
                tag.ColorHex,
                canManage ? counts.GetValueOrDefault(tag.KurinTagKey) : null))
            .ToList();

        return new ServiceResult<KurinTagsDto>(ResultType.Success,
            new KurinTagsDto(true, canManage, KurinTagRules.MaxTagsPerMember, KurinTagRules.Palette, items));
    }
}
