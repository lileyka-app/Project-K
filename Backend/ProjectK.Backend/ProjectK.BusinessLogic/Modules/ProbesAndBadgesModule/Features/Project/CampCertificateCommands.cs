using MediatR;
using ProjectK.BusinessLogic.Behaviors;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Project;

/// <summary>
/// Attaches the camp's посвідка, a picture or a PDF, replacing the one before. A picture is
/// re-encoded like any photo; a PDF is kept only when its bytes say it is one.
/// </summary>
public sealed record UploadCampCertificateCommand(
    Guid MemberKey,
    string ProbeId,
    byte[] Content,
    string FileName,
    bool IsPdf) : IRequest<ServiceResult<ThirdProbeResponse>>, ITransactionalRequest;

/// <summary>Takes the посвідка off, file included.</summary>
public sealed record DeleteCampCertificateCommand(Guid MemberKey, string ProbeId) : IRequest<ServiceResult<ThirdProbeResponse>>;

/// <summary>What both посвідка handlers share: their dependencies and the journal line.</summary>
public abstract class CampCertificateHandlerBase
{
    protected static ServiceResult<ThirdProbeResponse> StorageFull() =>
        ProbeProjectGuard.Refuse<ThirdProbeResponse>(
            "StorageFull", $"The third probe's files would take more than {ProbeProjectRules.MaxStoredBytes / (1024 * 1024)} MB.");

    protected CampCertificateHandlerBase(
        IUnitOfWork unitOfWork,
        IProbeProjectAccess access,
        IPhotoService photos,
        IKurinAudit audit,
        IProbesCatalogService probes,
        IThirdProbeReader reader)
    {
        UnitOfWork = unitOfWork;
        Access = access;
        Photos = photos;
        Audit = audit;
        Probes = probes;
        Reader = reader;
    }

    protected IUnitOfWork UnitOfWork { get; }
    protected IProbeProjectAccess Access { get; }
    protected IPhotoService Photos { get; }
    protected IKurinAudit Audit { get; }
    protected IProbesCatalogService Probes { get; }
    protected IThirdProbeReader Reader { get; }

    protected Task RecordAsync(string action, ThirdProbeFile file, Guid memberKey, string probeId, CancellationToken cancellationToken) =>
        Audit.RecordAsync(
            new AuditRecord(AuditArea.Probe, action, file.ThirdProbeFileKey, ProgressAuditLabels.Probe(Probes, probeId), memberKey),
            cancellationToken);
}

public sealed class UploadCampCertificateCommandHandler
    : CampCertificateHandlerBase, IRequestHandler<UploadCampCertificateCommand, ServiceResult<ThirdProbeResponse>>
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

    public async Task<ServiceResult<ThirdProbeResponse>> Handle(UploadCampCertificateCommand request, CancellationToken cancellationToken)
    {
        var probeId = request.ProbeId.Trim();
        if (await ProbeProjectGuard.RefuseAsync<ThirdProbeResponse>(Access, request.MemberKey, probeId, cancellationToken) is { } refused)
        {
            return refused;
        }

        if (request.Content.LongLength > ProbeProjectRules.MaxCertificateBytes)
        {
            return ProbeProjectGuard.Refuse<ThirdProbeResponse>("CertificateTooLarge", "The посвідка must be 2 MB or smaller.");
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
            return ProbeProjectGuard.Refuse<ThirdProbeResponse>(
                "InvalidCertificate", "The посвідка must be a picture (PNG, JPEG, WebP) or a PDF.");
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
            return new ServiceResult<ThirdProbeResponse>(ResultType.InternalServerError);
        }

        if (previous is not null)
        {
            await Photos.DeletePhotoAsync(previous, cancellationToken);
        }

        return new ServiceResult<ThirdProbeResponse>(ResultType.Success, await Reader.ReadAsync(request.MemberKey, probeId, cancellationToken));
    }
}

public sealed class DeleteCampCertificateCommandHandler
    : CampCertificateHandlerBase, IRequestHandler<DeleteCampCertificateCommand, ServiceResult<ThirdProbeResponse>>
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

    public async Task<ServiceResult<ThirdProbeResponse>> Handle(DeleteCampCertificateCommand request, CancellationToken cancellationToken)
    {
        var probeId = request.ProbeId.Trim();
        if (await ProbeProjectGuard.RefuseAsync<ThirdProbeResponse>(Access, request.MemberKey, probeId, cancellationToken) is { } refused)
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

        return new ServiceResult<ThirdProbeResponse>(ResultType.Success, await Reader.ReadAsync(request.MemberKey, probeId, cancellationToken));
    }
}
