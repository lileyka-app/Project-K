using MediatR;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

/// <summary>
/// A senior kurin's emblem, shown where a УПЮ kurin shows its red number plate. The plate is an
/// юнацтво відзнака (BRANDBOOK §7), so a УСП or УПС kurin uploads its own picture instead; a УПЮ
/// kurin keeps the plate and cannot upload one. Same types and size as a гурток's silhouette.
/// </summary>
public sealed record UploadKurinEmblemCommand(Guid KurinKey, byte[] BlobContent, string BlobFileName)
    : IRequest<ServiceResult<KurinDto>>;

public sealed class UploadKurinEmblemCommandHandler : IRequestHandler<UploadKurinEmblemCommand, ServiceResult<KurinDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPhotoService _photoService;
    private readonly IBlobReadLinks _links;
    private readonly IBackendCache _cache;
    private readonly IMembershipDirectory _memberships;

    public UploadKurinEmblemCommandHandler(IUnitOfWork unitOfWork, IPhotoService photoService, IBlobReadLinks links, IBackendCache cache, IMembershipDirectory memberships)
    {
        _unitOfWork = unitOfWork;
        _photoService = photoService;
        _links = links;
        _cache = cache;
        _memberships = memberships;
    }

    public async Task<ServiceResult<KurinDto>> Handle(UploadKurinEmblemCommand request, CancellationToken cancellationToken)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return KurinErrors.NotFound<KurinDto>();
        }

        if (!KurinBranchSetRules.IsSenior(kurin.Branch))
        {
            return ServiceResult<KurinDto>.Failure(ResultType.BadRequest, KurinErrors.EmblemForSeniorKurins, "A УПЮ kurin keeps its number plate.");
        }

        var oldBlobName = kurin.EmblemBlobName;
        PhotoUploadResult upload;
        try
        {
            upload = await _photoService.UploadPhotoAsync(request.BlobContent, request.BlobFileName, BlobUploadContext.KurinEmblem, cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ServiceResult<KurinDto>.Failure(ResultType.BadRequest, KurinErrors.InvalidImageContent, "Uploaded file is not a valid image.");
        }

        kurin.EmblemBlobName = upload.BlobName;
        _unitOfWork.Kurins.Update(kurin, cancellationToken);
        if (await _unitOfWork.SaveChangesAsync(cancellationToken) <= 0)
        {
            await _photoService.DeletePhotoAsync(upload.BlobName, cancellationToken);
            return ServiceResult<KurinDto>.Failure(ResultType.InternalServerError, KurinErrors.KurinSaveFailed, "The kurin was not saved.");
        }

        // The replaced picture goes now; anything that slips through is the orphan cleanup's.
        if (!string.IsNullOrWhiteSpace(oldBlobName) && oldBlobName != upload.BlobName)
        {
            await _photoService.DeletePhotoAsync(oldBlobName, cancellationToken);
        }

        _cache.Invalidate(BackendCacheRules.KurinReads);
        return new ServiceResult<KurinDto>(ResultType.Success, kurin.ToDto(_links, await _memberships.CountCurrentInKurinAsync(kurin.KurinKey, cancellationToken)));
    }
}
