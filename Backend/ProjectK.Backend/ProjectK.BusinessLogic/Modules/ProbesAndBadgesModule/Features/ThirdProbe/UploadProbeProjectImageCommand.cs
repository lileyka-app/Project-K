using MediatR;
using ProjectK.BusinessLogic.Behaviors;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.ThirdProbe;

/// <summary>
/// Stores a picture for a project's description and hands back its key and a link. The picture is
/// the project's from now on; it stays only if the next save of the description shows it.
/// </summary>
public sealed record UploadProbeProjectImageCommand(
    Guid MemberKey,
    string ProbeId,
    Guid ProjectKey,
    byte[] Content,
    string FileName) : IRequest<ServiceResult<ProbeProjectImageDto>>, ITransactionalRequest;

public sealed class UploadProbeProjectImageCommandHandler
    : IRequestHandler<UploadProbeProjectImageCommand, ServiceResult<ProbeProjectImageDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbesAndBadgesAccess _access;
    private readonly IPhotoService _photos;
    private readonly IBlobReadLinks _links;

    public UploadProbeProjectImageCommandHandler(
        IUnitOfWork unitOfWork,
        IProbesAndBadgesAccess access,
        IPhotoService photos,
        IBlobReadLinks links)
    {
        _unitOfWork = unitOfWork;
        _access = access;
        _photos = photos;
        _links = links;
    }

    public async Task<ServiceResult<ProbeProjectImageDto>> Handle(UploadProbeProjectImageCommand request, CancellationToken cancellationToken)
    {
        var probeId = request.ProbeId.Trim();
        if (await ProbeProjectWriteRules.RefuseAsync<ProbeProjectImageDto>(_access, request.MemberKey, probeId, cancellationToken) is { } refused)
        {
            return refused;
        }

        if (request.Content.LongLength > ProbeProjectRules.MaxImageBytes)
        {
            return ServiceResult<ProbeProjectImageDto>.Failure(
                ResultType.BadRequest, ProbesAndBadgesErrors.ImageTooLarge, "Image file must be 1 MB or smaller.");
        }

        await _unitOfWork.ProbeProjects.LockAsync(request.MemberKey, probeId, cancellationToken);
        var project = await _unitOfWork.ProbeProjects.GetWithImagesAsync(request.ProjectKey, cancellationToken);
        if (project is null || project.MemberKey != request.MemberKey || project.ProbeId != probeId)
        {
            return ServiceResult<ProbeProjectImageDto>.Failure(ResultType.NotFound, ProbesAndBadgesErrors.ProjectNotFound, "No such project in this probe.");
        }

        // Counted by rows, not by what the text shows: a picture taken out of the editor but not
        // yet saved still holds its place until the description is saved without it.
        if (project.Images.Count >= ProbeProjectRules.MaxImagesPerProject)
        {
            return ServiceResult<ProbeProjectImageDto>.Failure(
                ResultType.BadRequest, ProbesAndBadgesErrors.TooManyImages, $"A project holds at most {ProbeProjectRules.MaxImagesPerProject} pictures.");
        }

        var stored = await _unitOfWork.ProbeProjects.GetStoredBytesAsync(request.MemberKey, probeId, cancellationToken);
        if (stored >= ProbeProjectRules.MaxStoredBytes)
        {
            return StorageFull();
        }

        PhotoUploadResult upload;
        try
        {
            upload = await _photos.UploadPhotoAsync(
                request.Content, request.FileName, BlobUploadContext.ProbeProjectImage, cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ServiceResult<ProbeProjectImageDto>.Failure(
                ResultType.BadRequest, ProbesAndBadgesErrors.InvalidImageContent, "Uploaded file is not a valid image.");
        }

        // Counted as stored: the picture is re-encoded, and that, not the upload, is what is paid for.
        if (stored + upload.Bytes > ProbeProjectRules.MaxStoredBytes)
        {
            await _photos.DeletePhotoAsync(upload.BlobName, cancellationToken);
            return StorageFull();
        }

        var image = new ProbeProjectImage { ProbeProjectKey = project.ProbeProjectKey, BlobName = upload.BlobName, Bytes = upload.Bytes };
        _unitOfWork.ProbeProjects.AddImage(image);

        if (await _unitOfWork.SaveChangesAsync(cancellationToken) <= 0)
        {
            await _photos.DeletePhotoAsync(upload.BlobName, cancellationToken);
            return ServiceResult<ProbeProjectImageDto>.Failure(ResultType.InternalServerError, ProbesAndBadgesErrors.NotSaved, "The third probe was not saved.");
        }

        return new ServiceResult<ProbeProjectImageDto>(ResultType.Success, ThirdProbeMapping.ToDto(image, _links));
    }

    private static ServiceResult<ProbeProjectImageDto> StorageFull() =>
        ServiceResult<ProbeProjectImageDto>.Failure(
            ResultType.BadRequest, ProbesAndBadgesErrors.StorageFull, $"The third probe's files would take more than {ProbeProjectRules.MaxStoredBytes / (1024 * 1024)} MB.");
}
