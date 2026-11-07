using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Project;

/// <summary>
/// Removes a project with its pictures, blobs included. A main project that goes leaves its place to
/// the spare, which is what the spare is for — unless the spare is shared and two shared main
/// projects are already there: then it stays the spare.
/// </summary>
public sealed record DeleteProbeProjectCommand(Guid MemberKey, string ProbeId, Guid ProjectKey) : IRequest<ServiceResult<ThirdProbeResponse>>;

public sealed class DeleteProbeProjectCommandHandler : IRequestHandler<DeleteProbeProjectCommand, ServiceResult<ThirdProbeResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbeProjectAccess _access;
    private readonly IPhotoService _photos;
    private readonly IKurinAudit _audit;
    private readonly IProbesCatalogService _probes;
    private readonly IThirdProbeReader _reader;

    public DeleteProbeProjectCommandHandler(
        IUnitOfWork unitOfWork,
        IProbeProjectAccess access,
        IPhotoService photos,
        IKurinAudit audit,
        IProbesCatalogService probes,
        IThirdProbeReader reader)
    {
        _unitOfWork = unitOfWork;
        _access = access;
        _photos = photos;
        _audit = audit;
        _probes = probes;
        _reader = reader;
    }

    public async Task<ServiceResult<ThirdProbeResponse>> Handle(DeleteProbeProjectCommand request, CancellationToken cancellationToken)
    {
        var probeId = request.ProbeId.Trim();
        if (await ProbeProjectGuard.RefuseAsync<ThirdProbeResponse>(_access, request.MemberKey, probeId, cancellationToken) is { } refused)
        {
            return refused;
        }

        var all = await _unitOfWork.ProbeProjects.GetForMemberProbeAsync(request.MemberKey, probeId, cancellationToken);
        var project = all.FirstOrDefault(p => p.ProbeProjectKey == request.ProjectKey);
        if (project is null)
        {
            return new ServiceResult<ThirdProbeResponse>(ResultType.NotFound);
        }

        var blobs = project.Images.Select(image => image.BlobName).ToList();
        _unitOfWork.ProbeProjects.Delete(project, cancellationToken);
        if (!project.IsSpare
            && all.FirstOrDefault(p => p.IsSpare) is { } spare
            && (!spare.IsShared
                || all.Count(p => p != project && !p.IsSpare && p.IsShared) < ProbeProjectRules.MaxSharedMainProjects))
        {
            spare.IsSpare = false;
        }

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Probe,
                AuditActions.Probe.ProjectRemoved,
                project.ProbeProjectKey,
                ProgressAuditLabels.Probe(_probes, probeId),
                request.MemberKey,
                AuditChanges.Removed(("project", project.Title))),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var blob in blobs)
        {
            await _photos.DeletePhotoAsync(blob, cancellationToken);
        }

        return new ServiceResult<ThirdProbeResponse>(ResultType.Success, await _reader.ReadAsync(request.MemberKey, probeId, cancellationToken));
    }
}
