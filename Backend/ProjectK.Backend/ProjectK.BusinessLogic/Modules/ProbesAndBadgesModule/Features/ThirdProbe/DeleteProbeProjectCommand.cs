using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.ThirdProbe;

/// <summary>
/// Removes a project with its pictures, blobs included. A main project that goes leaves its place to
/// the spare, which is what the spare is for — unless the spare is shared and two shared main
/// projects are already there: then it stays the spare.
/// </summary>
public sealed record DeleteProbeProjectCommand(Guid MemberKey, string ProbeId, Guid ProjectKey) : IRequest<ServiceResult<ThirdProbeDto>>;

public sealed class DeleteProbeProjectCommandHandler : IRequestHandler<DeleteProbeProjectCommand, ServiceResult<ThirdProbeDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbeProjectAccess _access;
    private readonly IPhotoService _photos;
    private readonly IKurinAudit _audit;
    private readonly IProbesCatalogReader _probes;
    private readonly IThirdProbeReader _reader;

    public DeleteProbeProjectCommandHandler(
        IUnitOfWork unitOfWork,
        IProbeProjectAccess access,
        IPhotoService photos,
        IKurinAudit audit,
        IProbesCatalogReader probes,
        IThirdProbeReader reader)
    {
        _unitOfWork = unitOfWork;
        _access = access;
        _photos = photos;
        _audit = audit;
        _probes = probes;
        _reader = reader;
    }

    public async Task<ServiceResult<ThirdProbeDto>> Handle(DeleteProbeProjectCommand request, CancellationToken cancellationToken)
    {
        var probeId = request.ProbeId.Trim();
        if (await ProbeProjectGuard.RefuseAsync<ThirdProbeDto>(_access, request.MemberKey, probeId, cancellationToken) is { } refused)
        {
            return refused;
        }

        var all = await _unitOfWork.ProbeProjects.GetForMemberProbeAsync(request.MemberKey, probeId, cancellationToken);
        var project = all.FirstOrDefault(p => p.ProbeProjectKey == request.ProjectKey);
        if (project is null)
        {
            return ServiceResult<ThirdProbeDto>.Failure(ResultType.NotFound, ProbesAndBadgesErrors.ProjectNotFound, "No such project in this probe.");
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

        return new ServiceResult<ThirdProbeDto>(ResultType.Success, await _reader.ReadAsync(request.MemberKey, probeId, cancellationToken));
    }
}
