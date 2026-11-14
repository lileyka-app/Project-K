using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using CatalogBadge = ProjectK.Common.Dtos.ProbesAndBadgesModule.Badge;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;

/// <summary>One badge definition by its catalogue id.</summary>
public sealed record GetBadgeQuery(string Id) : IRequest<ServiceResult<CatalogBadge>>;

public sealed class GetBadgeQueryHandler : IRequestHandler<GetBadgeQuery, ServiceResult<CatalogBadge>>
{
    private readonly IBadgesCatalogReader _catalog;

    public GetBadgeQueryHandler(IBadgesCatalogReader catalog)
    {
        _catalog = catalog;
    }

    public Task<ServiceResult<CatalogBadge>> Handle(GetBadgeQuery request, CancellationToken cancellationToken)
    {
        var badge = _catalog.GetBadgeById(request.Id);
        return Task.FromResult(badge is null
            ? ServiceResult<CatalogBadge>.Failure(ResultType.NotFound, ProbesAndBadgesErrors.BadgeNotFound, "No badge with this id is in the catalogue.")
            : new ServiceResult<CatalogBadge>(ResultType.Success, badge));
    }
}
