using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using CatalogBadge = ProjectK.Common.Dtos.ProbesAndBadgesModule.Badge;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;

/// <summary>The вмілості a kurin's members may start, by its край's profile.</summary>
public sealed record GetKurinBadgesQuery(Guid KurinKey) : IRequest<ServiceResult<IReadOnlyList<CatalogBadge>>>;

public sealed class GetKurinBadgesQueryHandler : IRequestHandler<GetKurinBadgesQuery, ServiceResult<IReadOnlyList<CatalogBadge>>>
{
    private readonly IKurinCatalog _catalog;

    public GetKurinBadgesQueryHandler(IKurinCatalog catalog)
    {
        _catalog = catalog;
    }

    public async Task<ServiceResult<IReadOnlyList<CatalogBadge>>> Handle(GetKurinBadgesQuery request, CancellationToken cancellationToken)
    {
        var catalog = await _catalog.ForAsync(request.KurinKey, cancellationToken);
        return catalog is null
            ? ServiceResult<IReadOnlyList<CatalogBadge>>.Failure(ResultType.NotFound, ProbesAndBadgesErrors.KurinNotFound, "Kurin not found.")
            : new ServiceResult<IReadOnlyList<CatalogBadge>>(ResultType.Success, catalog.Badges);
    }
}
