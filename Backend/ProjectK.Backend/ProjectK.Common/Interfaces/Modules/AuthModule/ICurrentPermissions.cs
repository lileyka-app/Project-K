using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Interfaces.Modules.AuthModule;

/// <summary>
/// The current request's permissions in the kurin its token is scoped to, the kurin's policy
/// included. Worked out once per request and kept: every check of one request sees the same answer,
/// and a policy saved a moment ago is seen by the very next request with the same token.
/// </summary>
public interface ICurrentPermissions
{
    /// <summary>Every permission the caller holds here.</summary>
    Task<IReadOnlyCollection<Permission>> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>The widest scope at which the caller holds (resource, action), if any.</summary>
    Task<AccessScope?> WidestScopeAsync(ResourceType resource, ResourceAction action, CancellationToken cancellationToken = default);

    /// <summary>
    /// How far the kurin's policy lets the caller exercise <paramref name="capability"/>: the widest
    /// reach over the audiences their roles fold into. Leveled capabilities are asked one level at a time.
    /// </summary>
    Task<CapabilityReach> ReachAsync(KurinCapability capability, CancellationToken cancellationToken = default);
}
