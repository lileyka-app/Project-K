using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe.Get;

/// <summary>The probes a member walks, in order: their <see cref="ProbeLine"/>.</summary>
public sealed record GetMemberProbeLineQuery(Guid MemberKey) : IRequest<ServiceResult<IReadOnlyList<ProbeSummaryResponse>>>;

public sealed class GetMemberProbeLineQueryHandler
    : IRequestHandler<GetMemberProbeLineQuery, ServiceResult<IReadOnlyList<ProbeSummaryResponse>>>
{
    private readonly IMemberProbeLine _line;

    public GetMemberProbeLineQueryHandler(IMemberProbeLine line)
    {
        _line = line;
    }

    public async Task<ServiceResult<IReadOnlyList<ProbeSummaryResponse>>> Handle(GetMemberProbeLineQuery request, CancellationToken cancellationToken)
    {
        return new ServiceResult<IReadOnlyList<ProbeSummaryResponse>>(
            ResultType.Success,
            await _line.ForMemberAsync(request.MemberKey, cancellationToken));
    }
}
