using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Records.IdentityModule;

namespace ProjectK.Common.Rules.IdentityModule;

/// <summary>
/// Permission-derived questions handlers ask about the current user, so no handler inspects role
/// names directly. These read the static grants of <see cref="RolePermissionRules"/> only and know
/// nothing of the kurin's policy: a pair listed in <see cref="KurinCapabilityRules"/> is asked through
/// <c>ICurrentPermissions</c> or <c>IResourceAccessService</c> instead.
/// </summary>
public static class CurrentUserPermissionExtensions
{
    public static bool IsAdmin(this ICurrentUserContext user) => user.IsInRole(SystemRoleNames.Admin);

    /// <summary>The grants that do not depend on the kurin's policy.</summary>
    public static IReadOnlySet<Permission> StaticPermissions(this ICurrentUserContext user) =>
        RolePermissionRules.StaticGrants(user.Roles ?? Array.Empty<string>());

    /// <summary>True for the whole-kurin managers (Зв'язковий and admin; a Курінний leads but does not manage).</summary>
    public static bool CanManageWholeKurin(this ICurrentUserContext user) =>
        RolePermissionRules.GrantsWholeKurinManagement(user.Roles ?? Array.Empty<string>());

    /// <summary>True for anyone who leads groups (гуртковий leaders plus whole-kurin managers).</summary>
    public static bool CanLeadGroups(this ICurrentUserContext user) =>
        RolePermissionRules.GrantsGroupLeadership(user.Roles ?? Array.Empty<string>());

    /// <summary>Whole-kurin managers or group leaders — the historic "leadership" set.</summary>
    public static bool IsLeadership(this ICurrentUserContext user) =>
        user.CanManageWholeKurin() || user.CanLeadGroups();

    /// <summary>
    /// Whether the user is granted (resource, action) at any scope. Throws for a pair the kurin's
    /// policy decides: answered from the static map alone it would be wrong in every kurin that
    /// changed it, so asking it here is a bug, not a question.
    /// </summary>
    public static bool HasPermission(this ICurrentUserContext user, ResourceType resource, ResourceAction action)
    {
        if (KurinCapabilityRules.IsConfigurable(resource, action))
        {
            throw new ArgumentException(
                $"{resource}:{action} is set by the kurin's policy; ask ICurrentPermissions instead.",
                nameof(resource));
        }

        return RolePermissionRules.WidestScope(user.StaticPermissions(), resource, action) is not null;
    }
}
