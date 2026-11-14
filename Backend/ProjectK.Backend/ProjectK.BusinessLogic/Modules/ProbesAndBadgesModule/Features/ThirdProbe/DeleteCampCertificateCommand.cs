using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.ThirdProbe;

/// <summary>Takes the посвідка off, file included.</summary>
public sealed record DeleteCampCertificateCommand(Guid MemberKey, string ProbeId) : IRequest<ServiceResult<ThirdProbeDto>>;

public sealed class DeleteCampCertificateCommandHandler
    : CampCertificateHandlerBase, IRequestHandler<DeleteCampCertificateCommand, ServiceResult<ThirdProbeDto>>
{
    public DeleteCampCertificateCommandHandler(
        IUnitOfWork unitOfWork,
        IProbeProjectAccess access,
        IPhotoService photos,
        IKurinAudit audit,
        IProbesCatalogService probes,
        IThirdProbeReader reader)
        : base(unitOfWork, access, photos, audit, probes, reader)
    {
    }

    public async Task<ServiceResult<ThirdProbeDto>> Handle(DeleteCampCertificateCommand request, CancellationToken cancellationToken)
    {
        var probeId = request.ProbeId.Trim();
        if (await ProbeProjectGuard.RefuseAsync<ThirdProbeDto>(Access, request.MemberKey, probeId, cancellationToken) is { } refused)
        {
            return refused;
        }

        var file = await UnitOfWork.ProbeProjects.GetFileAsync(request.MemberKey, probeId, cancellationToken);
        if (file?.CampCertificateBlobName is { } blob)
        {
            file.CampCertificateBlobName = null;
            file.CampCertificateContentType = null;
            file.CampCertificateFileName = null;
            file.CampCertificateBytes = 0;
            await RecordAsync(AuditActions.Probe.CertificateRemoved, file, request.MemberKey, probeId, cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);
            await Photos.DeletePhotoAsync(blob, cancellationToken);
        }

        return new ServiceResult<ThirdProbeDto>(ResultType.Success, await Reader.ReadAsync(request.MemberKey, probeId, cancellationToken));
    }
}
