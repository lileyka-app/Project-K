using Microsoft.AspNetCore.Mvc;
using ProjectK.Common.Enums.IdentityModule;

namespace ProjectK.API.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ResourceAuthorizeAttribute : TypeFilterAttribute
{
    public ResourceAuthorizeAttribute(ResourceType resourceType, ResourceAction action, string resourceKeySelector)
        : base(typeof(ResourceAuthorizeFilter))
    {
        Arguments = [true, resourceType, string.Empty, action, resourceKeySelector, false, default(ResourceType)];
    }

    /// <summary>
    /// Checks <paramref name="resourceType"/> but reads the scope from <paramref name="scopeOf"/> at
    /// <paramref name="resourceKeySelector"/> — for routes that identify the owner rather than the
    /// record, such as signing a probe point by member key.
    /// </summary>
    public ResourceAuthorizeAttribute(
        ResourceType resourceType,
        ResourceAction action,
        string resourceKeySelector,
        ResourceType scopeOf)
        : base(typeof(ResourceAuthorizeFilter))
    {
        Arguments = [true, resourceType, string.Empty, action, resourceKeySelector, true, scopeOf];
    }

    public ResourceAuthorizeAttribute(string resourceTypeSelector, ResourceAction action, string resourceKeySelector)
        : base(typeof(ResourceAuthorizeFilter))
    {
        Arguments = [false, default(ResourceType), resourceTypeSelector, action, resourceKeySelector, false, default(ResourceType)];
    }
}
