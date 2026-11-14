using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Models.Catalog;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;

/// <summary>The badge catalogue's categories and levels, for building filters.</summary>
public sealed record GetBadgesMetadataQuery : IRequest<ServiceResult<BadgesMetadata>>;

public sealed class GetBadgesMetadataQueryHandler : IRequestHandler<GetBadgesMetadataQuery, ServiceResult<BadgesMetadata>>
{
    private readonly IBadgesCatalogService _catalog;

    public GetBadgesMetadataQueryHandler(IBadgesCatalogService catalog)
    {
        _catalog = catalog;
    }

    public Task<ServiceResult<BadgesMetadata>> Handle(GetBadgesMetadataQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ServiceResult<BadgesMetadata>(ResultType.Success, _catalog.GetBadgesMetadata()));
    }
}
