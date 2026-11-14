using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Extensions;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.API.Uploads;

/// <summary>
/// The image check every upload endpoint makes before it reads a byte, in one place. Each endpoint
/// used to carry its own copy of the null, size and type checks.
/// </summary>
public static class ImageUploadExtensions
{
    public const string MissingImageCode = "MissingImage";
    public const string MissingImageMessage = "Image file is required.";

    /// <summary>
    /// Refuses an image that came but is too large or of a type the API does not take. A missing
    /// image passes: use this where the picture is optional.
    /// </summary>
    public static IActionResult? RefuseImage(
        this ControllerBase controller,
        IFormFile? file,
        long maxBytes = ImageUploadRules.MaxImageBytes)
    {
        return ImageUploadRules.Refusal(file, maxBytes) is { } refusal
            ? controller.Failure(ResultType.BadRequest, refusal.Code, refusal.Message)
            : null;
    }

    /// <summary>
    /// Refuses a missing or empty image with <paramref name="missingCode"/>, and a wrong one as
    /// <see cref="RefuseImage"/> does.
    /// </summary>
    public static IActionResult? RefuseRequiredImage(
        this ControllerBase controller,
        IFormFile? file,
        string missingCode = MissingImageCode,
        string missingMessage = MissingImageMessage,
        long maxBytes = ImageUploadRules.MaxImageBytes)
    {
        if (file is null || file.Length == 0)
        {
            return controller.Failure(ResultType.BadRequest, missingCode, missingMessage);
        }

        return controller.RefuseImage(file, maxBytes);
    }

    /// <summary>
    /// Checks a required image, reads it, and answers with what <paramref name="send"/> makes of its
    /// bytes and file name.
    /// </summary>
    public static async Task<IActionResult> SendImageAsync<T>(
        this ControllerBase controller,
        IFormFile? file,
        Func<byte[], string, Task<ServiceResult<T>>> send,
        CancellationToken cancellationToken,
        long maxBytes = ImageUploadRules.MaxImageBytes)
    {
        if (controller.RefuseRequiredImage(file, maxBytes: maxBytes) is { } refusal)
        {
            return refusal;
        }

        var bytes = await file.ToByteArrayAsync(cancellationToken);
        if (bytes is null || bytes.Length == 0)
        {
            return controller.Failure(ResultType.BadRequest, MissingImageCode, MissingImageMessage);
        }

        var result = await send(bytes, file!.FileName);
        return result.ToActionResult(controller);
    }
}
