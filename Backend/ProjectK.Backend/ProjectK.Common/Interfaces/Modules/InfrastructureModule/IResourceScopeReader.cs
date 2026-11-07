using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Interfaces.Modules.InfrastructureModule;

/// <summary>
/// Reads only the keys an authorization decision needs. The entity repositories load a
/// full graph for editing; ResourceAuthorizeFilter runs on nearly every request, so it
/// gets its own projection instead.
/// </summary>
public interface IResourceScopeReader
{
    /// <summary>
    /// The scope of a resource <b>as seen from</b> <paramref name="inKurinKey"/>. The caller's
    /// kurin is part of the question because a person can belong to several: their гурток, and
    /// whether they are reachable at all, is only meaningful within one of them. Null means the
    /// resource does not exist there, which is the same answer as it not existing.
    /// </summary>
    Task<ResourceScope?> GetScopeAsync(
        ResourceType resourceType,
        Guid resourceKey,
        Guid inKurinKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the person a resource is about stands in a УСП or УПС kurin now and is no longer a
    /// юнак anywhere — no open юнацьке membership in an УПЮ kurin. Their probes,
    /// вмілості and УПЮ відзначення are then a closed youth record, which the rules guard harder.
    /// </summary>
    Task<bool> StandsInSeniorKurinAsync(
        ResourceType resourceType,
        Guid resourceKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The account of the person a resource is about — a person, their data, their progress — and
    /// null for anything else or a person without an account. Asked without a kurin: a person's
    /// own record is theirs whether or not they stand in any kurin today.
    /// </summary>
    Task<Guid?> GetPersonAccountKeyAsync(
        ResourceType resourceType,
        Guid resourceKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Groups the user leads in this kurin: active гуртковий offices, explicit mentor assignments
    /// (legacy) and the group they are a member of. This is the "own groups" scope tier.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetLedGroupKeysAsync(
        Guid userKey,
        Guid kurinKey,
        CancellationToken cancellationToken = default);
}
