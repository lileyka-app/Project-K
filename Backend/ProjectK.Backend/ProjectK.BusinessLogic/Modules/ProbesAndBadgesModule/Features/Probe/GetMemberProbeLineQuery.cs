using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe;

/// <summary>The probes a member walks, in order: their <see cref="ProbeLineRules"/>.</summary>
public sealed record GetMemberProbeLineQuery(Guid MemberKey) : IRequest<ServiceResult<IReadOnlyList<ProbeSummaryDto>>>;

public sealed class GetMemberProbeLineQueryHandler
    : IRequestHandler<GetMemberProbeLineQuery, ServiceResult<IReadOnlyList<ProbeSummaryDto>>>
{
    private readonly IMemberProbeLine _line;

    public GetMemberProbeLineQueryHandler(IMemberProbeLine line)
    {
        _line = line;
    }

    public async Task<ServiceResult<IReadOnlyList<ProbeSummaryDto>>> Handle(GetMemberProbeLineQuery request, CancellationToken cancellationToken)
    {
        return new ServiceResult<IReadOnlyList<ProbeSummaryDto>>(
            ResultType.Success,
            await _line.ForMemberAsync(request.MemberKey, cancellationToken));
    }
}
