using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;

public sealed class UploadGroupSilhouetteCommand : IRequest<ServiceResult<GroupResponse>>
{
    public Guid GroupKey { get; }
    public byte[] BlobContent { get; }
    public string BlobFileName { get; }

    public UploadGroupSilhouetteCommand(Guid groupKey, byte[] blobContent, string blobFileName)
    {
        GroupKey = groupKey;
        BlobContent = blobContent;
        BlobFileName = blobFileName;
    }
}

public sealed class UploadGroupSilhouetteCommandHandler : IRequestHandler<UploadGroupSilhouetteCommand, ServiceResult<GroupResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPhotoService _photoService;
    private readonly IMapper _mapper;
    private readonly IBackendCache _cache;

    public UploadGroupSilhouetteCommandHandler(
        IUnitOfWork unitOfWork,
        IPhotoService photoService,
        IMapper mapper,
        IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _photoService = photoService;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<ServiceResult<GroupResponse>> Handle(UploadGroupSilhouetteCommand request, CancellationToken cancellationToken)
    {
        var group = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey, cancellationToken);
        if (group == null)
        {
            return new ServiceResult<GroupResponse>(ResultType.NotFound);
        }

        var oldBlobName = group.SilhouetteBlobName;
        PhotoUploadResult upload;

        try
        {
            upload = await _photoService.UploadPhotoAsync(
                request.BlobContent,
                request.BlobFileName,
                BlobUploadContext.GroupSilhouette,
                cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ServiceResult<GroupResponse>.Failure(
                ResultType.BadRequest,
                KurinErrors.InvalidImageContent,
                "Uploaded file is not a valid image.");
        }

        group.SilhouetteBlobName = upload.BlobName;
        _unitOfWork.Groups.Update(group, cancellationToken);

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            await _photoService.DeletePhotoAsync(upload.BlobName, cancellationToken);
            return new ServiceResult<GroupResponse>(ResultType.InternalServerError);
        }

        if (!string.IsNullOrWhiteSpace(oldBlobName) && oldBlobName != upload.BlobName)
        {
            await _photoService.DeletePhotoAsync(oldBlobName, cancellationToken);
        }

        _cache.Invalidate(BackendCachePolicies.GroupReads);
        return new ServiceResult<GroupResponse>(ResultType.Success, _mapper.Map<GroupResponse>(group));
    }
}
