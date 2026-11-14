using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.Common.Records.AuthModule;

/// <summary>
/// A single granted capability: an <see cref="ResourceAction"/> on a <see cref="ResourceType"/>
/// at a given <see cref="AccessScope"/>. Permissions are the one authorization vocabulary shared
/// by <c>ResourceAccessService</c>, the request policies and the frontend — roles map to a set of
/// these via <see cref="RolePermissionRules"/>, and nothing checks role names directly.
/// </summary>
public readonly record struct Permission(ResourceType Resource, ResourceAction Action, AccessScope Scope)
{
    /// <summary>Stable string form (<c>Resource:Action:Scope</c>) for JWT claims and the frontend.</summary>
    public string ToClaimValue() => $"{Resource}:{Action}:{Scope}";
}
