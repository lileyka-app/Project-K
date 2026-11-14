using MediatR;
using ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;

/// <summary>Every probe definition in the catalogue.</summary>
public sealed record GetProbesQuery : IRequest<ServiceResult<IReadOnlyList<ProbeSummaryDto>>>;

public sealed class GetProbesQueryHandler : IRequestHandler<GetProbesQuery, ServiceResult<IReadOnlyList<ProbeSummaryDto>>>
{
    private readonly IProbesCatalogService _catalog;

    public GetProbesQueryHandler(IProbesCatalogService catalog)
    {
        _catalog = catalog;
    }

    public Task<ServiceResult<IReadOnlyList<ProbeSummaryDto>>> Handle(GetProbesQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ServiceResult<IReadOnlyList<ProbeSummaryDto>>(ResultType.Success, _catalog.GetProbes()));
    }
}
