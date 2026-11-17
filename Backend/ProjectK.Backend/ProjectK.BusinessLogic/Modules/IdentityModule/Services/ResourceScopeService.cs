using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Records.NotificationsModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Services;

/// <summary>
/// The <b>where</b> half of <see cref="ResourceAccessService"/>: whether a granted
/// <see cref="AccessScope"/> reaches a resource in the kurin the current user acts in — the whole
/// kurin, the гуртки they lead, or only their own record.
/// </summary>
public sealed class ResourceScopeService
{
    private readonly IResourceScopeRepository _scopeRepository;
    private readonly IResourceScopeReader _scopeReader;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IBackendCache _cache;

    public ResourceScopeService(
        IResourceScopeRepository scopeRepository,
        IResourceScopeReader scopeReader,
        ICurrentUserContext currentUserContext,
        IBackendCache cache)
    {
        _scopeRepository = scopeRepository;
        _scopeReader = scopeReader;
        _currentUserContext = currentUserContext;
        _cache = cache;
    }

    /// <summary>True when the resource is the current user's own record.</summary>
    public async Task<bool> IsCallersOwnAsync(ResourceType resourceType, Guid resourceKey, CancellationToken cancellationToken)
    {
        if (_currentUserContext.UserId is not { } userKey)
        {
            return false;
        }

        return await _scopeRepository.GetPersonAccountKeyAsync(resourceType, resourceKey, cancellationToken) == userKey;
    }

    /// <summary>
    /// Whether <paramref name="grantedScope"/> reaches the resource, which must also stand in the kurin
    /// the current user acts in.
    /// </summary>
    public async Task<ResourceAccessDecision> CheckAsync(
        AccessScope grantedScope,
        ResourceType scopeResourceType,
        Guid scopeResourceKey,
        CancellationToken cancellationToken)
    {
        var currentKurinKey = _currentUserContext.KurinKey;
        if (currentKurinKey is null)
        {
            return ResourceAccessDecision.Deny("Current user does not have kurin scope claim.");
        }

        var scope = await _scopeRepository.GetScopeAsync(scopeResourceType, scopeResourceKey, currentKurinKey.Value, cancellationToken);
        if (scope is null)
        {
            return ResourceAccessDecision.Deny("Resource was not found or has no resolvable scope.");
        }

        var scopeDecision = await EvaluateAsync(grantedScope, scope, currentKurinKey.Value, cancellationToken);
        if (!scopeDecision.IsAllowed)
        {
            return scopeDecision;
        }

        if (scope.KurinKey != currentKurinKey.Value)
        {
            return ResourceAccessDecision.Deny("Resource belongs to a different kurin scope.");
        }

        return ResourceAccessDecision.Allow("Permission and resource scope checks passed.");
    }

    /// <summary>
    /// Whether <paramref name="grantedScope"/> reaches a resource already resolved to
    /// <paramref name="scope"/>; the kurin it stands in is the caller's to compare.
    /// </summary>
    public async Task<ResourceAccessDecision> EvaluateAsync(
        AccessScope grantedScope,
        ResourceScope scope,
        Guid currentKurinKey,
        CancellationToken cancellationToken)
    {
        switch (grantedScope)
        {
            case AccessScope.KurinWide:
                return ResourceAccessDecision.Allow("Kurin-wide permission; validating kurin scope.");

            case AccessScope.Own:
                return ValidateOwnership(scope);

            case AccessScope.OwnGroups:
                return await ValidateOwnGroupsAsync(scope, currentKurinKey, cancellationToken);

            default:
                return ResourceAccessDecision.Deny("Unknown access scope.");
        }
    }

    private ResourceAccessDecision ValidateOwnership(ResourceScope scope)
    {
        var currentUserId = _currentUserContext.UserId;
        if (currentUserId is null)
        {
            return ResourceAccessDecision.Deny("Current user id claim is missing.");
        }

        if (!scope.MemberUserKey.HasValue || scope.MemberUserKey.Value != currentUserId.Value)
        {
            return ResourceAccessDecision.Deny("Permission is limited to own resources.");
        }

        return ResourceAccessDecision.Allow("Ownership check passed.");
    }

    private async Task<ResourceAccessDecision> ValidateOwnGroupsAsync(
        ResourceScope scope,
        Guid currentKurinKey,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserContext.UserId;
        if (currentUserId is null)
        {
            return ResourceAccessDecision.Deny("Current user id claim is missing.");
        }

        // Managing one's own record is always allowed within a group-scoped grant.
        if (scope.MemberUserKey.HasValue && scope.MemberUserKey.Value == currentUserId.Value)
        {
            return ResourceAccessDecision.Allow("Own record within group-scoped permission.");
        }

        var ledGroupKeys = await _cache.GetOrCreateAsync(
            BackendCacheRules.MentorScopeReads,
            $"ledgroups:kurin:{currentKurinKey}",
            token => _scopeReader.GetLedGroupKeysAsync(currentUserId.Value, currentKurinKey, token),
            cancellationToken,
            CacheScopeContext.From(_currentUserContext));

        if (ledGroupKeys.Count == 0)
        {
            return ResourceAccessDecision.Deny("No groups are led by the current user.");
        }

        var reachesLedGroup =
            (scope.GroupKey.HasValue && ledGroupKeys.Contains(scope.GroupKey.Value))
            || (scope.GroupKeys is not null && scope.GroupKeys.Any(ledGroupKeys.Contains));

        if (!reachesLedGroup)
        {
            return ResourceAccessDecision.Deny("Permission is limited to led groups.");
        }

        return ResourceAccessDecision.Allow("Led-group scope check passed.");
    }
}
