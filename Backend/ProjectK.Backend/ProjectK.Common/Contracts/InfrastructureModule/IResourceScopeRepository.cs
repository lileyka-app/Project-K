using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Records.AuthModule;
using ProjectK.Common.Records.InfrastructureModule;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.Common.Contracts.InfrastructureModule;

/// <summary>
/// Reads only the keys an authorization decision needs. The entity repositories load a
/// full graph for editing; ResourceAuthorizeFilter runs on nearly every request, so it
/// gets its own projection instead.
/// </summary>
public interface IResourceScopeRepository
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
    /// The open memberships of the person a resource is about — a person, their data, their progress —
    /// one row per гілка of each kurin; empty for anything else. Whether they make the person a
    /// senior is <c>SeniorStandingRules</c>' to say.
    /// </summary>
    Task<IReadOnlyList<MembershipBranchRow>> GetOpenMembershipBranchesAsync(
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
    /// What the user holds over гуртки in this kurin, each source apart: active гуртковий offices,
    /// mentor assignments in force and the group they are a member of. Which of them the "own groups"
    /// tier counts is <c>LedGroupRules</c>' to say.
    /// </summary>
    Task<LedGroupSourcesRow> GetLedGroupSourcesAsync(
        Guid userKey,
        Guid kurinKey,
        CancellationToken cancellationToken = default);
}
