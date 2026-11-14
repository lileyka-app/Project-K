namespace ProjectK.API.Models.Requests;

/// <summary>
/// One uploaded image, bound from the multipart form.
/// <para>
/// A model rather than a bare <see cref="IFormFile"/> parameter: Swashbuckle refuses to describe
/// <c>[FromForm] IFormFile</c> and fails the whole document, while dropping the attribute makes MVC
/// reject an anonymous request with 415 before authentication has had its say — which would tell a
/// caller the endpoint exists. Binding through a model keeps the 401 and describes the form.
/// </para>
/// </summary>
public class UploadImageRequest
{
    public IFormFile? File { get; set; }
}
