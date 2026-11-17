using MediatR;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using CatalogBadge = ProjectK.Common.Dtos.ProbesAndBadgesModule.Badge;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;

/// <summary>Badge definitions, the first <paramref name="Take"/> of them.</summary>
public sealed record GetBadgesQuery(int Take) : IRequest<ServiceResult<IReadOnlyList<CatalogBadge>>>;

public sealed class GetBadgesQueryHandler : IRequestHandler<GetBadgesQuery, ServiceResult<IReadOnlyList<CatalogBadge>>>
{
    public const int DefaultPage = 200;
    public const int MaxPage = 500;

    private readonly IBadgesCatalogReader _catalog;

    public GetBadgesQueryHandler(IBadgesCatalogReader catalog)
    {
        _catalog = catalog;
    }

    public Task<ServiceResult<IReadOnlyList<CatalogBadge>>> Handle(GetBadgesQuery request, CancellationToken cancellationToken)
    {
        // The catalog is a few hundred entries; a page past that is the whole thing, and a
        // non-positive request is a mistake answered with the default rather than nothing.
        var page = request.Take <= 0 ? DefaultPage : Math.Min(request.Take, MaxPage);
        return Task.FromResult(new ServiceResult<IReadOnlyList<CatalogBadge>>(ResultType.Success, _catalog.GetBadges(page)));
    }
}
