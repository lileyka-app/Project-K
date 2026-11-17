using System.Security.Claims;
using ProjectK.BusinessLogic.Modules.IdentityModule;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.API.Middleware;

/// <summary>
/// Refuses a mutation from a whole-kurin manager whose account has no second factor, where the
/// policy demands one. Answered from the access token's <c>amr</c> claim: it used to look the
/// account up on every such request for one bit, and the token already knows.
/// </summary>
public class PrivilegedMfaEnforcementMiddleware
{
    private readonly RequestDelegate _next;

    public PrivilegedMfaEnforcementMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IMfaEnforcementReader mfaEnforcementReader)
    {
        if (!RequiresMfaEnforcement(context))
        {
            await _next(context);
            return;
        }

        if (!await mfaEnforcementReader.IsPrivilegedMfaRequiredAsync(context.RequestAborted))
        {
            await _next(context);
            return;
        }

        if (context.User.HasSecondFactor())
        {
            await _next(context);
            return;
        }

        // The standard failure shape; the clients tell this refusal from any other 403 by the code.
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            error = IdentityErrors.PrivilegedMfaRequired,
            message = "MFA is required for privileged accounts."
        }, context.RequestAborted);
    }

    private static bool RequiresMfaEnforcement(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsGet(context.Request.Method)
            || context.User.Identity?.IsAuthenticated != true
            || IsExemptPath(context))
        {
            return false;
        }

        // Privileged = whole-kurin managers: Зв'язковий and admin. Курінний leads the провід but
        // holds nothing on members, so he is not privileged here.
        return RolePermissionRules.GrantsWholeKurinManagement(
            context.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value));
    }

    private static bool IsExemptPath(PathString path)
    {
        return path.StartsWithSegments("/api/auth/mfa", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
            // Local-tier testing aids; the controller behind them does not exist on deployed tiers.
            || path.StartsWithSegments("/api/dev", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/auth/refresh", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExemptPath(HttpContext context)
    {
        var path = context.Request.Path;
        if (IsExemptPath(path))
        {
            return true;
        }

        return (HttpMethods.IsPost(context.Request.Method)
                && path.StartsWithSegments("/api/user/me/mfa/reset", StringComparison.OrdinalIgnoreCase))
            || (HttpMethods.IsPost(context.Request.Method)
                && path.StartsWithSegments("/api/auth/check-access", StringComparison.OrdinalIgnoreCase));
    }
}
