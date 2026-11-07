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
    /// The guard on a senior's youth record, for what one endpoint shares with other records: an
    /// УПЮ відзначення beside a senior one, ступені beside the rest of the profile. Allowed when the
    /// person stands in no УСП or УПС kurin. Otherwise only who holds the kurin-wide right in the kurin
    /// they stand in now, and the person themself only where <paramref name="ownerMayChange"/>.
    /// Probes, signed or not, nobody changes. Where the change is a capability the kurin sets level
    /// by level — confirming an award of one level — <paramref name="capability"/> names it, and its
    /// reach counts rather than the widest of the permission it shares.
    /// </summary>
    Task<ResourceAccessDecision> CheckSeniorYouthRecordAsync(
        ResourceType resourceType,
        ResourceAction action,
        Guid memberKey,
        bool ownerMayChange = false,
        CancellationToken cancellationToken = default,
        KurinCapability? capability = null);

    /// <summary>
    /// Every capability the caller may exercise on the resource, worked out from one read of its
    /// scope. Empty when the resource is out of reach altogether.
    /// </summary>
    Task<IReadOnlySet<KurinCapability>> ReachableCapabilitiesAsync(
        ResourceType scopeResourceType,
        Guid scopeResourceKey,
        CancellationToken cancellationToken = default);
}
