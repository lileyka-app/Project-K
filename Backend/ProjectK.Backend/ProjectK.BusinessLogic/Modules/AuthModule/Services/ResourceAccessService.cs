using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <summary>
/// The single authorization decision point. It answers two orthogonal questions: <b>what</b> the
/// user may do (their <see cref="Permission"/> set, resolved from roles and the kurin's policy by
/// <see cref="ICurrentPermissions"/>) and <b>where</b> it applies (the <see cref="AccessScope"/> of
/// the widest matching permission, checked against the resource's kurin/group/owner). Role names
/// are never inspected here beyond the system-admin bypass.
/// </summary>
public class ResourceAccessService : IResourceAccessService
{
    private readonly IResourceScopeReader _scopeReader;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IBackendCache _cache;
    private readonly ICurrentPermissions _permissions;

    public ResourceAccessService(
        IResourceScopeReader scopeReader,
        ICurrentUserContext currentUserContext,
        IBackendCache cache,
        ICurrentPermissions permissions)
    {
        _scopeReader = scopeReader;
        _currentUserContext = currentUserContext;
        _cache = cache;
        _permissions = permissions;
    }

    public Task<ResourceAccessDecision> CheckAccessAsync(
        ResourceType resourceType,
        ResourceAction action,
        Guid resourceKey,
        CancellationToken cancellationToken = default)
        => CheckAccessAsync(resourceType, action, resourceType, resourceKey, cancellationToken);

    public async Task<ResourceAccessDecision> CheckAccessAsync(
        ResourceType resourceType,
        ResourceAction action,
        ResourceType scopeResourceType,
        Guid scopeResourceKey,
        CancellationToken cancellationToken = default)
    {
        if (Precheck() is { } early)
        {
            return early;
        }

        var grantedScope = await _permissions.WidestScopeAsync(resourceType, action, cancellationToken);
        if (grantedScope is null)
        {
            return ResourceAccessDecision.Deny($"No permission for {action} on {resourceType}.");
        }

        return await CheckScopeAsync(grantedScope.Value, scopeResourceType, scopeResourceKey, cancellationToken);
    }

    public async Task<ResourceAccessDecision> CheckCapabilityAsync(
        KurinCapability capability,
        ResourceType scopeResourceType,
        Guid scopeResourceKey,
        CancellationToken cancellationToken = default)
    {
        if (Precheck() is { } early)
        {
            return early;
        }

        var grantedScope = (await _permissions.ReachAsync(capability, cancellationToken)).ToScope();
        if (grantedScope is null)
        {
            return ResourceAccessDecision.Deny($"The kurin's policy does not give {capability}.");
        }

        return await CheckScopeAsync(grantedScope.Value, scopeResourceType, scopeResourceKey, cancellationToken);
    }

    public async Task<IReadOnlySet<KurinCapability>> ReachableCapabilitiesAsync(
        ResourceType scopeResourceType,
        Guid scopeResourceKey,
        CancellationToken cancellationToken = default)
    {
        var reachable = new HashSet<KurinCapability>();
        if (Precheck() is { } early)
        {
            if (early.IsAllowed)
            {
                reachable.UnionWith(KurinCapabilities.All.Select(descriptor => descriptor.Key));
            }

            return reachable;
        }

        if (_currentUserContext.KurinKey is not { } currentKurinKey)
        {
            return reachable;
        }

        var scope = await _scopeReader.GetScopeAsync(scopeResourceType, scopeResourceKey, currentKurinKey, cancellationToken);
        if (scope is null || scope.KurinKey != currentKurinKey)
        {
            return reachable;
        }

        foreach (var descriptor in KurinCapabilities.All)
        {
            var grantedScope = (await _permissions.ReachAsync(descriptor.Key, cancellationToken)).ToScope();
            if (grantedScope is not null
                && (await EvaluateScopeAsync(grantedScope.Value, scope, currentKurinKey, cancellationToken)).IsAllowed)
            {
                reachable.Add(descriptor.Key);
            }
        }

        return reachable;
    }

    /// <summary>
    /// The answer reached before any permission is looked at: deny for nobody, allow for an admin
    /// with no kurin, null to go on.
    /// </summary>
    private ResourceAccessDecision? Precheck()
    {
        if (!_currentUserContext.IsAuthenticated)
        {
            return ResourceAccessDecision.Deny("User is not authenticated.");
        }

        // An unscoped admin is system-wide: that is the /panel view, where there is no kurin to
        // check against. Once they step into a kurin the claim is re-issued and they are held to
        // that scope like anyone else — otherwise browser history reaches other kurins' data.
        if (_currentUserContext.IsInRole(SystemRole.Admin) && _currentUserContext.KurinKey is null)
        {
            return ResourceAccessDecision.Allow("Admin bypass: no kurin scope selected.");
        }

        return null;
    }

    private async Task<ResourceAccessDecision> CheckScopeAsync(
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

        var scope = await _scopeReader.GetScopeAsync(scopeResourceType, scopeResourceKey, currentKurinKey.Value, cancellationToken);
        if (scope is null)
        {
            return ResourceAccessDecision.Deny("Resource was not found or has no resolvable scope.");
        }

        var scopeDecision = await EvaluateScopeAsync(grantedScope, scope, currentKurinKey.Value, cancellationToken);
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

    private async Task<ResourceAccessDecision> EvaluateScopeAsync(
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
            BackendCachePolicies.MentorScopeReads,
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
