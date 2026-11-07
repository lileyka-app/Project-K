using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Emblem;

/// <summary>
/// A senior kurin's emblem, shown where a УПЮ kurin shows its red number plate. The plate is an
/// юнацтво відзнака (BRANDBOOK §7), so a УСП or УПС kurin uploads its own picture instead; a УПЮ
/// kurin keeps the plate and cannot upload one. Same types and size as a гурток's silhouette.
/// </summary>
public sealed record UploadKurinEmblemCommand(Guid KurinKey, byte[] BlobContent, string BlobFileName)
    : IRequest<ServiceResult<KurinResponse>>;

/// <summary>Takes the emblem off; the kurin is shown with its plain placeholder again.</summary>
public sealed record DeleteKurinEmblemCommand(Guid KurinKey) : IRequest<ServiceResult<KurinResponse>>;

public sealed class UploadKurinEmblemCommandHandler : IRequestHandler<UploadKurinEmblemCommand, ServiceResult<KurinResponse>>
{
    public const string NotSeniorCode = "EmblemForSeniorKurins";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPhotoService _photoService;
    private readonly IMapper _mapper;
    private readonly IBackendCache _cache;

    public UploadKurinEmblemCommandHandler(IUnitOfWork unitOfWork, IPhotoService photoService, IMapper mapper, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _photoService = photoService;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<ServiceResult<KurinResponse>> Handle(UploadKurinEmblemCommand request, CancellationToken cancellationToken)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return new ServiceResult<KurinResponse>(ResultType.NotFound);
        }

        if (!KurinBranchSet.IsSenior(kurin.Branch))
        {
            return ServiceResult<KurinResponse>.Failure(ResultType.BadRequest, NotSeniorCode, "A УПЮ kurin keeps its number plate.");
        }

        var oldBlobName = kurin.EmblemBlobName;
        PhotoUploadResult upload;
        try
        {
            upload = await _photoService.UploadPhotoAsync(request.BlobContent, request.BlobFileName, BlobUploadContext.KurinEmblem, cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ServiceResult<KurinResponse>.Failure(ResultType.BadRequest, "InvalidImageContent", "Uploaded file is not a valid image.");
        }

        kurin.EmblemBlobName = upload.BlobName;
        _unitOfWork.Kurins.Update(kurin, cancellationToken);
        if (await _unitOfWork.SaveChangesAsync(cancellationToken) <= 0)
        {
            await _photoService.DeletePhotoAsync(upload.BlobName, cancellationToken);
            return new ServiceResult<KurinResponse>(ResultType.InternalServerError);
        }

        // The replaced picture goes now; anything that slips through is the orphan cleanup's.
        if (!string.IsNullOrWhiteSpace(oldBlobName) && oldBlobName != upload.BlobName)
        {
            await _photoService.DeletePhotoAsync(oldBlobName, cancellationToken);
        }

        _cache.Invalidate(BackendCachePolicies.KurinReads);
        return new ServiceResult<KurinResponse>(ResultType.Success, _mapper.Map<KurinResponse>(kurin));
    }
}

public sealed class DeleteKurinEmblemCommandHandler : IRequestHandler<DeleteKurinEmblemCommand, ServiceResult<KurinResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPhotoService _photoService;
    private readonly IMapper _mapper;
    private readonly IBackendCache _cache;

    public DeleteKurinEmblemCommandHandler(IUnitOfWork unitOfWork, IPhotoService photoService, IMapper mapper, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _photoService = photoService;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<ServiceResult<KurinResponse>> Handle(DeleteKurinEmblemCommand request, CancellationToken cancellationToken)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return new ServiceResult<KurinResponse>(ResultType.NotFound);
        }

        var oldBlobName = kurin.EmblemBlobName;
        if (string.IsNullOrWhiteSpace(oldBlobName))
        {
            return new ServiceResult<KurinResponse>(ResultType.Success, _mapper.Map<KurinResponse>(kurin));
        }

        kurin.EmblemBlobName = null;
        _unitOfWork.Kurins.Update(kurin, cancellationToken);
        if (await _unitOfWork.SaveChangesAsync(cancellationToken) <= 0)
        {
            return new ServiceResult<KurinResponse>(ResultType.InternalServerError);
        }

        await _photoService.DeletePhotoAsync(oldBlobName, cancellationToken);
        _cache.Invalidate(BackendCachePolicies.KurinReads);
        return new ServiceResult<KurinResponse>(ResultType.Success, _mapper.Map<KurinResponse>(kurin));
    }
}
