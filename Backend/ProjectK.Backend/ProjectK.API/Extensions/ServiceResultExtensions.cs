using Microsoft.AspNetCore.Mvc;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Extensions;

/// <summary>
/// Turns a <see cref="ServiceResult{T}"/> into an HTTP response. Lives in the API project because
/// deciding that "not found" means 404 is a transport decision, not a domain one — it used to sit in
/// ProjectK.Common and drag Microsoft.AspNetCore.Mvc into the innermost project.
/// <para>
/// Every failure body is <c>{ error, message }</c>. Controllers that hand-rolled their own responses
/// produced three other shapes, so the same client-side handler saw a different payload depending on
/// which endpoint failed.
/// </para>
/// </summary>
public static class ServiceResultExtensions
{
    public static IActionResult ToActionResult<T>(this ServiceResult<T> result, ControllerBase controller)
    {
        if (result.ErrorCode != null || result.ErrorMessage != null)
        {
            var errorResponse = new { error = result.ErrorCode, message = result.ErrorMessage };
            return result.Type switch
            {
                ResultType.BadRequest => controller.BadRequest(errorResponse),
                ResultType.Unauthorized => controller.StatusCode(StatusCodes.Status401Unauthorized, errorResponse),
                ResultType.NotFound => controller.NotFound(errorResponse),
                ResultType.Conflict => controller.Conflict(errorResponse),
                ResultType.Forbidden => controller.StatusCode(StatusCodes.Status403Forbidden, errorResponse),
                ResultType.TooManyRequests => controller.StatusCode(StatusCodes.Status429TooManyRequests, errorResponse),
                _ => controller.StatusCode(StatusCodes.Status500InternalServerError, errorResponse)
            };
        }

        // A failure that carries neither a code nor a payload used to fall through to a bare status
        // result, which [ApiController] then rendered as RFC-9110 ProblemDetails — a body with no
        // `message` at all. A failed login landed there, so the UI showed its own transport error
        // instead of "wrong email or password".
        if (EqualityComparer<T?>.Default.Equals(result.Data, default) && result.Type is not (ResultType.Success or ResultType.Created))
        {
            var (code, message) = DefaultFailure(result.Type);
            return ServiceResult<object>.Failure(result.Type, code, message).ToActionResult(controller);
        }

        return result.Type switch
        {
            ResultType.Success => controller.Ok(result.Data),
            ResultType.Created => result.CreatedAtActionName != null
                ? controller.CreatedAtAction(result.CreatedAtActionName, result.CreatedAtRouteValues, result.Data)
                : controller.Created(string.Empty, result.Data),
            ResultType.BadRequest => controller.BadRequest(result.Data),
            ResultType.NotFound => controller.NotFound(result.Data),
            ResultType.Conflict => controller.Conflict(new object[] { "The entity that was attempted to be created already exists.", result.Data! }),
            ResultType.Forbidden => controller.StatusCode(StatusCodes.Status403Forbidden, result.Data),
            _ => controller.StatusCode(StatusCodes.Status500InternalServerError, result.Data),
        };
    }

    private static (string Code, string Message) DefaultFailure(ResultType type) => type switch
    {
        ResultType.BadRequest => ("BadRequest", "The request could not be processed."),
        ResultType.Unauthorized => ("Unauthorized", "Authentication is required or the credentials are invalid."),
        ResultType.Forbidden => ("Forbidden", "You do not have access to this resource."),
        ResultType.NotFound => ("NotFound", "The requested resource was not found."),
        ResultType.Conflict => ("Conflict", "The resource is in a conflicting state."),
        ResultType.UnprocessableEntity => ("UnprocessableEntity", "The request was understood but could not be processed."),
        ResultType.TooManyRequests => ("TooManyRequests", "Too many requests. Please try again later."),
        _ => ("InternalServerError", "An unexpected error occurred.")
    };

    /// <summary>
    /// A failure raised by the controller itself — a missing file, an unreadable claim — in the same
    /// shape a handler's failure would produce.
    /// </summary>
    public static IActionResult Failure(
        this ControllerBase controller,
        ResultType type,
        string errorCode,
        string errorMessage)
        => ServiceResult<object>.Failure(type, errorCode, errorMessage).ToActionResult(controller);

    /// <summary>The caller's identity claim is missing or unreadable.</summary>
    public static IActionResult UnreadableIdentity(this ControllerBase controller)
        => controller.Failure(ResultType.Unauthorized, "InvalidToken", "The access token carries no readable user identity.");
}
