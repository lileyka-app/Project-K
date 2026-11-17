using MediatR;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;

/// <summary>The badge catalogue's categories and levels, for building filters.</summary>
public sealed record GetBadgesMetadataQuery : IRequest<ServiceResult<BadgesMetadata>>;

public sealed class GetBadgesMetadataQueryHandler : IRequestHandler<GetBadgesMetadataQuery, ServiceResult<BadgesMetadata>>
{
    private readonly IBadgesCatalogReader _catalog;

    public GetBadgesMetadataQueryHandler(IBadgesCatalogReader catalog)
    {
        _catalog = catalog;
    }

    public Task<ServiceResult<BadgesMetadata>> Handle(GetBadgesMetadataQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ServiceResult<BadgesMetadata>(ResultType.Success, _catalog.GetBadgesMetadata()));
    }
}
