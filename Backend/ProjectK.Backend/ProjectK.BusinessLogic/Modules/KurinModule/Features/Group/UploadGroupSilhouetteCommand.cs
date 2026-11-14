using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;

public sealed class UploadGroupSilhouetteCommand : IRequest<ServiceResult<GroupDto>>
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

public sealed class UploadGroupSilhouetteCommandHandler : IRequestHandler<UploadGroupSilhouetteCommand, ServiceResult<GroupDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPhotoService _photoService;
    private readonly IBlobReadLinks _links;
    private readonly IBackendCache _cache;

    public UploadGroupSilhouetteCommandHandler(
        IUnitOfWork unitOfWork,
        IPhotoService photoService,
        IBlobReadLinks links,
        IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _photoService = photoService;
        _links = links;
        _cache = cache;
    }

    public async Task<ServiceResult<GroupDto>> Handle(UploadGroupSilhouetteCommand request, CancellationToken cancellationToken)
    {
        var group = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey, cancellationToken);
        if (group == null)
        {
            return KurinErrors.GroupMissing<GroupDto>();
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
            return ServiceResult<GroupDto>.Failure(
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
            return ServiceResult<GroupDto>.Failure(ResultType.InternalServerError, KurinErrors.GroupSaveFailed, "The group was not saved.");
        }

        if (!string.IsNullOrWhiteSpace(oldBlobName) && oldBlobName != upload.BlobName)
        {
            await _photoService.DeletePhotoAsync(oldBlobName, cancellationToken);
        }

        _cache.Invalidate(BackendCachePolicies.GroupReads);
        return new ServiceResult<GroupDto>(ResultType.Success, group.ToDto(_links));
    }
}
