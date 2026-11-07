using Microsoft.AspNetCore.Http;

namespace ProjectK.API.Helpers;

/// <summary>
/// What an uploaded picture has to look like before it is read — one rule for member photos and
/// group silhouettes alike. The image decoder is the real check; this only refuses at the door what
/// could not pass it, so a wrong file is not read into memory first.
/// </summary>
public static class ImageUploadRules
{
    /// <summary>The picture itself.</summary>
    public const long MaxImageBytes = 5 * 1024 * 1024;

    /// <summary>The whole multipart request: the picture plus the form fields around it.</summary>
    public const long MaxRequestBytes = 6 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp"
    };

    /// <summary>
    /// The error to answer with, or <c>null</c> when the file is acceptable or absent.
    /// </summary>
    public static (string Code, string Message)? Refusal(IFormFile? file) => Refusal(file, MaxImageBytes);

    /// <summary>The same rules with a smaller size limit, for pictures that live inside a text.</summary>
    public static (string Code, string Message)? Refusal(IFormFile? file, long maxBytes)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        if (file.Length > maxBytes)
        {
            return ("ImageTooLarge", $"Image file must be {FormatMegabytes(maxBytes)} MB or smaller.");
        }

        if (!AllowedContentTypes.Contains(file.ContentType ?? string.Empty))
        {
            return ("UnsupportedImageType", "Allowed image types are PNG, JPEG and WebP.");
        }

        return null;
    }

    private static string FormatMegabytes(long bytes) =>
        (bytes / (1024d * 1024d)).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
}
