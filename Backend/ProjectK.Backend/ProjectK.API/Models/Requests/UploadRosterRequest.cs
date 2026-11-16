namespace ProjectK.API.Models.Requests;

/// <summary>
/// The roster being uploaded. A model rather than a bare <see cref="IFormFile"/> for the same
/// reason as <see cref="UploadImageRequest"/>: Swashbuckle will not describe the bare form.
/// </summary>
public class UploadRosterRequest
{
    public IFormFile? File { get; set; }
}
