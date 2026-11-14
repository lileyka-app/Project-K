using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;

/// <summary>Every probe definition in the catalogue.</summary>
public sealed record GetProbesQuery : IRequest<ServiceResult<IReadOnlyList<ProbeSummaryResponse>>>;

public sealed class GetProbesQueryHandler : IRequestHandler<GetProbesQuery, ServiceResult<IReadOnlyList<ProbeSummaryResponse>>>
{
    private readonly IProbesCatalogService _catalog;

    public GetProbesQueryHandler(IProbesCatalogService catalog)
    {
        _catalog = catalog;
    }

    public Task<ServiceResult<IReadOnlyList<ProbeSummaryResponse>>> Handle(GetProbesQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ServiceResult<IReadOnlyList<ProbeSummaryResponse>>(ResultType.Success, _catalog.GetProbes()));
    }
}
