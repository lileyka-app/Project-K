using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ProjectK.Common.Models.Authorization;

namespace ProjectK.API.Authorization;

/// <summary>
/// The authorization policies, defined once.
/// <para>
/// They used to be spelled out in <c>Program.cs</c> and again in six integration-test hosts. A test
/// could therefore pass against policies that no longer matched production — which is precisely the
/// failure mode this release exists to remove.
/// </para>
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>System administrators.</summary>
    public const string RequireAdmin = "RequireAdmin";

    /// <summary>Any authenticated caller.</summary>
    public const string RequireUser = "RequireUser";

    /// <summary>Offices that manage the whole kurin.</summary>
    public const string RequireKurinManagement = "RequireKurinManagement";

    /// <summary>Offices that lead groups, whole-kurin management included.</summary>
    public const string RequireGroupLeadership = "RequireGroupLeadership";

    /// <summary>Offices allowed to author planning sessions.</summary>
    public const string RequirePlanningAuthor = "RequirePlanningAuthor";

    /// <summary>
    /// Registers every policy. The coarse gates are expressed as permissions so nothing checks role
    /// names; <c>ResourceAccessService</c> still applies scope per request on top of them.
    /// </summary>
    public static AuthorizationOptions AddProjectPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(RequireAdmin, policy => policy.RequireRole(SystemRole.Admin));


        options.AddPolicy(RequireKurinManagement, ByRoles(RolePermissionMap.GrantsWholeKurinManagement));
        options.AddPolicy(RequireGroupLeadership, ByRoles(RolePermissionMap.GrantsGroupLeadership));
        options.AddPolicy(RequirePlanningAuthor, ByRoles(RolePermissionMap.GrantsPlanningAuthoring));

        options.AddPolicy(RequireUser,
            policy => policy.RequireAssertion(context => context.User.Identity?.IsAuthenticated == true));

        return options;
    }

    private static Action<AuthorizationPolicyBuilder> ByRoles(Func<IEnumerable<string>, bool> grants) =>
        policy => policy.RequireAssertion(context =>
            grants(context.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value)));
}
