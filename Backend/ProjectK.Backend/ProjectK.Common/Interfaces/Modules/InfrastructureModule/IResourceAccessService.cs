using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Interfaces.Modules.InfrastructureModule;

public interface IResourceAccessService
{
    Task<ResourceAccessDecision> CheckAccessAsync(
        ResourceType resourceType,
        ResourceAction action,
        Guid resourceKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks <paramref name="resourceType"/> permission but reads the scope from a different entity.
    /// <para>
    /// Signing a probe point is a <c>ProbeProgress</c> permission, yet the route carries the member's
    /// key — the progress record may not exist yet. Without this the endpoints all borrowed
    /// <c>Member:Update</c>, which is how one permission came to cover thirteen operations.
    /// </para>
    /// </summary>
    Task<ResourceAccessDecision> CheckAccessAsync(
        ResourceType resourceType,
        ResourceAction action,
        ResourceType scopeResourceType,
        Guid scopeResourceKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the kurin's policy lets the caller exercise <paramref name="capability"/> on the
    /// resource: the reach it gives their audiences, checked against the resource's scope the same
    /// way a permission is. This is how a level is checked — a leveled capability shares its
    /// permission with the other levels, so the permission alone cannot tell them apart.
    /// </summary>
    Task<ResourceAccessDecision> CheckCapabilityAsync(
        KurinCapability capability,
        ResourceType scopeResourceType,
        Guid scopeResourceKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every capability the caller may exercise on the resource, worked out from one read of its
    /// scope. Empty when the resource is out of reach altogether.
    /// </summary>
    Task<IReadOnlySet<KurinCapability>> ReachableCapabilitiesAsync(
        ResourceType scopeResourceType,
        Guid scopeResourceKey,
        CancellationToken cancellationToken = default);
}
