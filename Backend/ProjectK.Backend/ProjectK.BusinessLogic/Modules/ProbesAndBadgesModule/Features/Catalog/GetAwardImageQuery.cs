using MediatR;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.ProbesAndBadgesModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;

/// <summary>The illustration for an award level, coloured or plain, as the catalogue publishes it.</summary>
public sealed record GetAwardImageQuery(int Level, bool Colored) : IRequest<ServiceResult<CatalogImage>>;

public sealed class GetAwardImageQueryHandler : IRequestHandler<GetAwardImageQuery, ServiceResult<CatalogImage>>
{
    private readonly IAwardImagesCatalog _images;

    public GetAwardImageQueryHandler(IAwardImagesCatalog images)
    {
        _images = images;
    }

    public async Task<ServiceResult<CatalogImage>> Handle(GetAwardImageQuery request, CancellationToken cancellationToken)
    {
        var image = await _images.GetAwardImageAsync(request.Level, request.Colored, cancellationToken);
        return image is null
            ? ServiceResult<CatalogImage>.Failure(ResultType.NotFound, ProbesAndBadgesErrors.AwardImageNotFound, "No award image exists for this level.")
            : new ServiceResult<CatalogImage>(ResultType.Success, image);
    }
}
