using MediatR;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;

/// <summary>The ступені and senior відзначення with their pictures, as the catalogue publishes them.</summary>
public sealed record GetHonoursQuery : IRequest<ServiceResult<IReadOnlyList<CatalogHonour>>>;

public sealed class GetHonoursQueryHandler : IRequestHandler<GetHonoursQuery, ServiceResult<IReadOnlyList<CatalogHonour>>>
{
    private readonly IHonoursCatalog _catalog;

    public GetHonoursQueryHandler(IHonoursCatalog catalog)
    {
        _catalog = catalog;
    }

    public Task<ServiceResult<IReadOnlyList<CatalogHonour>>> Handle(GetHonoursQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ServiceResult<IReadOnlyList<CatalogHonour>>(ResultType.Success, _catalog.GetAll()));
    }
}
