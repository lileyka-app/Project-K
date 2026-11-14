using MediatR;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.InfrastructureModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Features.Feedback;

/// <summary>
/// One picture for a problem report, stored before the report itself exists so the editor can
/// show it inline. Returns the public URL the report will embed.
/// </summary>
public sealed record UploadFeedbackScreenshotCommand(Stream Content, string FileName) : IRequest<ServiceResult<string>>;

public sealed class UploadFeedbackScreenshotCommandHandler
    : IRequestHandler<UploadFeedbackScreenshotCommand, ServiceResult<string>>
{
    private readonly IPhotoService _photoService;
    private readonly ICurrentUserContext _currentUser;

    public UploadFeedbackScreenshotCommandHandler(IPhotoService photoService, ICurrentUserContext currentUser)
    {
        _photoService = photoService;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<string>> Handle(UploadFeedbackScreenshotCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is null)
        {
            return ServiceResult<string>.Failure(ResultType.Unauthorized, InfrastructureErrors.SignInRequired, "Sign in to attach a screenshot.");
        }

        try
        {
            var upload = await _photoService.UploadPhotoAsync(
                request.Content,
                request.FileName,
                BlobUploadContext.FeedbackScreenshot,
                cancellationToken);

            return new ServiceResult<string>(ResultType.Success, upload.Url);
        }
        catch (InvalidOperationException)
        {
            return ServiceResult<string>.Failure(ResultType.BadRequest, InfrastructureErrors.InvalidImage, "The uploaded file is not a valid image.");
        }
    }
}
