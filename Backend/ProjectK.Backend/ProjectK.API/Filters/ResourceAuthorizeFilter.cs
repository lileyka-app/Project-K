using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Enums.IdentityModule;

namespace ProjectK.API.Filters;

public sealed class ResourceAuthorizeFilter : IAsyncActionFilter
{
    private readonly bool _useStaticResourceType;
    private readonly ResourceType _staticResourceType;
    private readonly string _resourceTypeSelector;
    private readonly ResourceAction _action;
    private readonly string _resourceKeySelector;
    private readonly bool _useScopeOverride;
    private readonly ResourceType _scopeResourceType;
    private readonly IResourceAccessService _resourceAccessService;

    public ResourceAuthorizeFilter(
        bool useStaticResourceType,
        ResourceType staticResourceType,
        string resourceTypeSelector,
        ResourceAction action,
        string resourceKeySelector,
        bool useScopeOverride,
        ResourceType scopeResourceType,
        IResourceAccessService resourceAccessService)
    {
        _useStaticResourceType = useStaticResourceType;
        _staticResourceType = staticResourceType;
        _resourceTypeSelector = resourceTypeSelector;
        _action = action;
        _resourceKeySelector = resourceKeySelector;
        _useScopeOverride = useScopeOverride;
        _scopeResourceType = scopeResourceType;
        _resourceAccessService = resourceAccessService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!TryResolveResourceType(context, out var resourceType, out var resourceTypeError))
        {
            context.Result = new BadRequestObjectResult(new { error = "InvalidResourceType", message = resourceTypeError });
            return;
        }

        if (!TryResolveGuid(context, _resourceKeySelector, out var resourceKey, out var resourceKeyError))
        {
            context.Result = new BadRequestObjectResult(new { error = "InvalidResourceKey", message = resourceKeyError });
            return;
        }

        var decision = await _resourceAccessService.CheckAccessAsync(
            resourceType,
            _action,
            _useScopeOverride ? _scopeResourceType : resourceType,
            resourceKey,
            context.HttpContext.RequestAborted);

        if (!decision.IsAllowed)
        {
            context.Result = new ObjectResult(new { error = "ResourceAccessDenied", message = decision.Reason })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };

            return;
        }

        await next();
    }

    private bool TryResolveResourceType(
        ActionExecutingContext context,
        out ResourceType resourceType,
        out string error)
    {
        if (_useStaticResourceType)
        {
            resourceType = _staticResourceType;
            error = string.Empty;
            return true;
        }

        if (string.IsNullOrWhiteSpace(_resourceTypeSelector))
        {
            resourceType = default;
            error = "Resource type selector is not configured.";
            return false;
        }

        if (!TryResolveString(context, _resourceTypeSelector, out var rawType, out error))
        {
            resourceType = default;
            return false;
        }

        if (TryParseResourceType(rawType, out resourceType))
        {
            error = string.Empty;
            return true;
        }

        error = $"Resource type '{rawType}' is not supported.";
        return false;
    }

    private static bool TryParseResourceType(string rawType, out ResourceType resourceType)
    {
        if (Enum.TryParse<ResourceType>(rawType, true, out resourceType))
        {
            return true;
        }

        return rawType.ToLowerInvariant() switch
        {
            "group" => SetResourceType(ResourceType.Group, out resourceType),
            "kurin" => SetResourceType(ResourceType.Kurin, out resourceType),
            "kv" => SetResourceType(ResourceType.Kurin, out resourceType),
            "member" => SetResourceType(ResourceType.Member, out resourceType),
            "leadership" => SetResourceType(ResourceType.Leadership, out resourceType),
            "planningsession" => SetResourceType(ResourceType.PlanningSession, out resourceType),
            _ => SetResourceType(default, out resourceType, false)
        };
    }

    private static bool SetResourceType(ResourceType type, out ResourceType resourceType, bool result = true)
    {
        resourceType = type;
        return result;
    }

    private static bool TryResolveGuid(
        ActionExecutingContext context,
        string selector,
        out Guid key,
        out string error)
    {
        if (!TryResolveValue(context, selector, out var rawValue, out error))
        {
            key = Guid.Empty;
            return false;
        }

        if (rawValue is Guid guid)
        {
            key = guid;
            return true;
        }

        if (Guid.TryParse(rawValue?.ToString(), out key))
        {
            return true;
        }

        error = $"Selector '{selector}' does not contain a valid Guid value.";
        return false;
    }

    private static bool TryResolveString(
        ActionExecutingContext context,
        string selector,
        out string value,
        out string error)
    {
        if (!TryResolveValue(context, selector, out var rawValue, out error))
        {
            value = string.Empty;
            return false;
        }

        value = rawValue?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"Selector '{selector}' resolved to empty value.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryResolveValue(
        ActionExecutingContext context,
        string selector,
        out object? value,
        out string error)
    {
        var alternatives = selector.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (alternatives.Length == 0)
        {
            value = null;
            error = "Resource selector is empty.";
            return false;
        }

        foreach (var singleSelector in alternatives)
        {
            if (TryResolveSingleValue(context, singleSelector, out value, out _))
            {
                error = string.Empty;
                return true;
            }
        }

        value = null;
        error = $"Unable to resolve selector '{selector}'.";
        return false;
    }

    private static bool TryResolveSingleValue(
        ActionExecutingContext context,
        string selector,
        out object? value,
        out string error)
    {
        if (TryResolveRouteSelector(context, selector, out value, out error))
        {
            return true;
        }

        if (TryResolveQuerySelector(context, selector, out value, out error))
        {
            return true;
        }

        return TryResolveArgumentPathSelector(context, selector, out value, out error);
    }

    private static bool TryResolveRouteSelector(
        ActionExecutingContext context,
        string selector,
        out object? value,
        out string error)
    {
        value = null;
        error = string.Empty;

        if (!selector.StartsWith("route:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var key = selector["route:".Length..];
        if (context.RouteData.Values.TryGetValue(key, out value) && value is not null)
        {
            return true;
        }

        error = $"Route value '{key}' was not found.";
        return false;
    }

    private static bool TryResolveQuerySelector(
        ActionExecutingContext context,
        string selector,
        out object? value,
        out string error)
    {
        value = null;
        error = string.Empty;

        if (!selector.StartsWith("query:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var key = selector["query:".Length..];
        if (context.HttpContext.Request.Query.TryGetValue(key, out var queryValue) &&
            !string.IsNullOrWhiteSpace(queryValue.FirstOrDefault()))
        {
            value = queryValue.FirstOrDefault();
            return true;
        }

        error = $"Query value '{key}' was not found.";
        return false;
    }

    private static bool TryResolveArgumentPathSelector(
        ActionExecutingContext context,
        string selector,
        out object? value,
        out string error)
    {

        var argumentPath = selector.StartsWith("arg:", StringComparison.OrdinalIgnoreCase)
            ? selector["arg:".Length..]
            : selector;

        var pathSegments = argumentPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pathSegments.Length == 0)
        {
            value = null;
            error = "Argument selector path is empty.";
            return false;
        }

        if (!context.ActionArguments.TryGetValue(pathSegments[0], out value) || value is null)
        {
            error = $"Action argument '{pathSegments[0]}' was not found.";
            return false;
        }

        for (var index = 1; index < pathSegments.Length; index++)
        {
            var currentSegment = pathSegments[index];
            var property = value.GetType().GetProperty(currentSegment);

            if (property is null)
            {
                error = $"Property '{currentSegment}' was not found on selector path '{selector}'.";
                return false;
            }

            value = property.GetValue(value);
            if (value is null)
            {
                error = $"Selector path '{selector}' resolved to null value.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
