using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;

namespace ProjectK.API.Middleware;

public sealed class SecurityActivityMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityActivityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IActivityLogger activityLogger)
    {
        if (context.User.GetUserKey() is { } userId)
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrWhiteSpace(ip))
            {
                activityLogger.TrackIpChange(userId, ip);
            }
        }

        await _next(context);
    }
}
