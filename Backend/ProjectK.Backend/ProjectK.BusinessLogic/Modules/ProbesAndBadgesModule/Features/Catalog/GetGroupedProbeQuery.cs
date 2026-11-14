using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;

/// <summary>One probe with its points grouped as they are presented in the book.</summary>
public sealed record GetGroupedProbeQuery(string ProbeId) : IRequest<ServiceResult<GroupedProbeDto>>;

public sealed class GetGroupedProbeQueryHandler : IRequestHandler<GetGroupedProbeQuery, ServiceResult<GroupedProbeDto>>
{
    private readonly IProbesCatalogService _catalog;

    public GetGroupedProbeQueryHandler(IProbesCatalogService catalog)
    {
        _catalog = catalog;
    }

    public Task<ServiceResult<GroupedProbeDto>> Handle(GetGroupedProbeQuery request, CancellationToken cancellationToken)
    {
        var probe = _catalog.GetGroupedProbeById(request.ProbeId);
        return Task.FromResult(probe is null
            ? ServiceResult<GroupedProbeDto>.Failure(ResultType.NotFound, ProbesAndBadgesErrors.ProbeNotFound, "No probe with this id is in the catalogue.")
            : new ServiceResult<GroupedProbeDto>(ResultType.Success, probe));
    }
}
