using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.ThirdProbe;

/// <summary>The «Третя проба» tab of a person: their file, projects and вмілості.</summary>
public sealed record GetThirdProbeQuery(Guid MemberKey, string ProbeId) : IRequest<ServiceResult<ThirdProbeDto>>;

public sealed class GetThirdProbeQueryHandler : IRequestHandler<GetThirdProbeQuery, ServiceResult<ThirdProbeDto>>
{
    private readonly IThirdProbeReader _reader;

    public GetThirdProbeQueryHandler(IThirdProbeReader reader)
    {
        _reader = reader;
    }

    public async Task<ServiceResult<ThirdProbeDto>> Handle(GetThirdProbeQuery request, CancellationToken cancellationToken) =>
        new(ResultType.Success, await _reader.ReadAsync(request.MemberKey, request.ProbeId.Trim(), cancellationToken));
}
