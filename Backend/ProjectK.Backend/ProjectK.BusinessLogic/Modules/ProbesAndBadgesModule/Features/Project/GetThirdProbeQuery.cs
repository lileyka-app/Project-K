using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Project;

/// <summary>The «Третя проба» tab of a person: their file, projects and вмілості.</summary>
public sealed record GetThirdProbeQuery(Guid MemberKey, string ProbeId) : IRequest<ServiceResult<ThirdProbeResponse>>;

public sealed class GetThirdProbeQueryHandler : IRequestHandler<GetThirdProbeQuery, ServiceResult<ThirdProbeResponse>>
{
    private readonly IThirdProbeReader _reader;

    public GetThirdProbeQueryHandler(IThirdProbeReader reader)
    {
        _reader = reader;
    }

    public async Task<ServiceResult<ThirdProbeResponse>> Handle(GetThirdProbeQuery request, CancellationToken cancellationToken) =>
        new(ResultType.Success, await _reader.ReadAsync(request.MemberKey, request.ProbeId.Trim(), cancellationToken));
}
