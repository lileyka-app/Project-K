using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.Common.Contracts.AuthModule;

/// <summary>
/// The two scope answers that are decisions rather than stored facts: which гуртки an account leads
/// and whether a person counts as a senior. It reads the facts from <c>IResourceScopeRepository</c>
/// and applies <c>LedGroupRules</c> and <c>SeniorStandingRules</c>, so every module asks here and
/// none repeats the rule.
/// </summary>
public interface IResourceScopeReader
{
    /// <summary>
    /// Groups the user leads in this kurin: active гуртковий offices, explicit mentor assignments
    /// (legacy) and the group they are a member of. This is the "own groups" scope tier.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetLedGroupKeysAsync(
        Guid userKey,
        Guid kurinKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the person a resource is about stands in a УСП or УПС kurin now and is no longer a
    /// юнак anywhere — no open юнацьке membership in an УПЮ kurin. False for a resource that is not
    /// about a person.
    /// </summary>
    Task<bool> StandsInSeniorKurinAsync(
        ResourceType resourceType,
        Guid resourceKey,
        CancellationToken cancellationToken = default);
}
