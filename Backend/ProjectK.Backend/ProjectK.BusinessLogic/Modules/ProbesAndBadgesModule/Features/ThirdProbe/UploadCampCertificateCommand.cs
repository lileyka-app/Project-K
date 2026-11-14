using MediatR;
using ProjectK.BusinessLogic.Behaviors;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.ThirdProbe;

/// <summary>
/// Attaches the camp's посвідка, a picture or a PDF, replacing the one before. A picture is
/// re-encoded like any photo; a PDF is kept only when its bytes say it is one.
/// </summary>
public sealed record UploadCampCertificateCommand(
    Guid MemberKey,
    string ProbeId,
    byte[] Content,
    string FileName,
    bool IsPdf) : IRequest<ServiceResult<ThirdProbeDto>>, ITransactionalRequest;

public sealed class UploadCampCertificateCommandHandler
    : CampCertificateHandlerBase, IRequestHandler<UploadCampCertificateCommand, ServiceResult<ThirdProbeDto>>
{
    public UploadCampCertificateCommandHandler(
        IUnitOfWork unitOfWork,
        IProbeProjectAccess access,
        IPhotoService photos,
        IKurinAudit audit,
        IProbesCatalogService probes,
        IThirdProbeReader reader)
        : base(unitOfWork, access, photos, audit, probes, reader)
    {
    }

    public async Task<ServiceResult<ThirdProbeDto>> Handle(UploadCampCertificateCommand request, CancellationToken cancellationToken)
    {
        var probeId = request.ProbeId.Trim();
        if (await ProbeProjectGuard.RefuseAsync<ThirdProbeDto>(Access, request.MemberKey, probeId, cancellationToken) is { } refused)
        {
            return refused;
        }

        if (request.Content.LongLength > ProbeProjectRules.MaxCertificateBytes)
        {
            return ProbeProjectGuard.Refuse<ThirdProbeDto>(ProbesAndBadgesErrors.CertificateTooLarge, "The посвідка must be 2 MB or smaller.");
        }

        await UnitOfWork.ProbeProjects.LockAsync(request.MemberKey, probeId, cancellationToken);
        var existing = await UnitOfWork.ProbeProjects.GetFileAsync(request.MemberKey, probeId, cancellationToken);
        var storedElsewhere = await UnitOfWork.ProbeProjects.GetStoredBytesAsync(request.MemberKey, probeId, cancellationToken)
                              - (existing?.CampCertificateBytes ?? 0);
        if (storedElsewhere >= ProbeProjectRules.MaxStoredBytes)
        {
            return StorageFull();
        }

        var context = request.IsPdf ? BlobUploadContext.ThirdProbeCertificatePdf : BlobUploadContext.ThirdProbeCertificateImage;
        PhotoUploadResult upload;
        try
        {
            upload = await Photos.UploadPhotoAsync(request.Content, request.FileName, context, cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ProbeProjectGuard.Refuse<ThirdProbeDto>(
                ProbesAndBadgesErrors.InvalidCertificate, "The посвідка must be a picture (PNG, JPEG, WebP) or a PDF.");
        }

        if (storedElsewhere + upload.Bytes > ProbeProjectRules.MaxStoredBytes)
        {
            await Photos.DeletePhotoAsync(upload.BlobName, cancellationToken);
            return StorageFull();
        }

        var file = await ThirdProbeFiles.GetOrAddAsync(UnitOfWork, request.MemberKey, probeId, cancellationToken);
        var previous = file.CampCertificateBlobName;
        file.CampCertificateBlobName = upload.BlobName;
        file.CampCertificateContentType = context.ContentType;
        file.CampCertificateBytes = upload.Bytes;
        file.CampCertificateFileName = Path.GetFileName(request.FileName) is { Length: > 0 and <= ProbeProjectRules.MaxShortTextLength } name ? name : null;

        await RecordAsync(AuditActions.Probe.CertificateAttached, file, request.MemberKey, probeId, cancellationToken);
        if (await UnitOfWork.SaveChangesAsync(cancellationToken) <= 0)
        {
            await Photos.DeletePhotoAsync(upload.BlobName, cancellationToken);
            return ServiceResult<ThirdProbeDto>.Failure(ResultType.InternalServerError, ProbesAndBadgesErrors.NotSaved, "The third probe was not saved.");
        }

        if (previous is not null)
        {
            await Photos.DeletePhotoAsync(previous, cancellationToken);
        }

        return new ServiceResult<ThirdProbeDto>(ResultType.Success, await Reader.ReadAsync(request.MemberKey, probeId, cancellationToken));
    }
}
