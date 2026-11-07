using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Project;

/// <summary>
/// Puts the spare project in place of a main one, which becomes the spare. Both stay as written;
/// only which one counts changes.
/// </summary>
public sealed record SwapSpareProjectCommand(Guid MemberKey, string ProbeId, Guid SpareKey, Guid MainKey)
    : IRequest<ServiceResult<ThirdProbeResponse>>;

public sealed class SwapSpareProjectCommandHandler : IRequestHandler<SwapSpareProjectCommand, ServiceResult<ThirdProbeResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbeProjectAccess _access;
    private readonly IKurinAudit _audit;
    private readonly IProbesCatalogService _probes;
    private readonly IThirdProbeReader _reader;

    public SwapSpareProjectCommandHandler(
        IUnitOfWork unitOfWork,
        IProbeProjectAccess access,
        IKurinAudit audit,
        IProbesCatalogService probes,
        IThirdProbeReader reader)
    {
        _unitOfWork = unitOfWork;
        _access = access;
        _audit = audit;
        _probes = probes;
        _reader = reader;
    }

    public async Task<ServiceResult<ThirdProbeResponse>> Handle(SwapSpareProjectCommand request, CancellationToken cancellationToken)
    {
        var probeId = request.ProbeId.Trim();
        if (await ProbeProjectGuard.RefuseAsync<ThirdProbeResponse>(_access, request.MemberKey, probeId, cancellationToken) is { } refused)
        {
            return refused;
        }

        var all = await _unitOfWork.ProbeProjects.GetForMemberProbeAsync(request.MemberKey, probeId, cancellationToken);
        var spare = all.FirstOrDefault(p => p.ProbeProjectKey == request.SpareKey && p.IsSpare);
        var main = all.FirstOrDefault(p => p.ProbeProjectKey == request.MainKey && !p.IsSpare);
        if (spare is null || main is null)
        {
            return new ServiceResult<ThirdProbeResponse>(ResultType.NotFound);
        }

        var sharedAfter = all.Count(p => p != main && !p.IsSpare && p.IsShared) + (spare.IsShared ? 1 : 0);
        if (sharedAfter > ProbeProjectRules.MaxSharedMainProjects)
        {
            return ProbeProjectGuard.Refuse<ThirdProbeResponse>(
                "TooManyShared", $"At most {ProbeProjectRules.MaxSharedMainProjects} of the main projects may be shared.");
        }

        spare.IsSpare = false;
        main.IsSpare = true;

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Probe,
                AuditActions.Probe.ProjectsSwapped,
                spare.ProbeProjectKey,
                ProgressAuditLabels.Probe(_probes, probeId),
                request.MemberKey,
                AuditChanges.Of(("project", main.Title, spare.Title))),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<ThirdProbeResponse>(ResultType.Success, await _reader.ReadAsync(request.MemberKey, probeId, cancellationToken));
    }
}
