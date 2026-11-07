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

        if (SignsOff(resourceType, action) && await IsCallersOwnAsync(scopeResourceType, scopeResourceKey, cancellationToken))
        {
            return ResourceAccessDecision.Deny(OwnSignOff);
        }

        var decision = await CheckScopeAsync(grantedScope.Value, scopeResourceType, scopeResourceKey, cancellationToken);
        if (decision.IsAllowed && ChangesYouthRecord(resourceType, action))
        {
            return await SeniorYouthRecordAsync(resourceType, grantedScope.Value, scopeResourceType, scopeResourceKey, ownerMayChange: false, cancellationToken)
                   ?? decision;
        }

        if (decision.IsAllowed || action != ResourceAction.Read)
        {
            return decision;
        }

        // A person released from their kurin, or not yet taken into the next one, stands nowhere —
        // and every scope above is a kurin's. Their own record they still read: the history is
        // theirs, not the kurin's. Reading only; changing it is the kurin's again once they stand in one.
        return await IsCallersOwnAsync(scopeResourceType, scopeResourceKey, cancellationToken)
            ? ResourceAccessDecision.Allow("Own record, read outside any kurin scope.")
            : decision;
    }

    private async Task<bool> IsCallersOwnAsync(ResourceType resourceType, Guid resourceKey, CancellationToken cancellationToken)
    {
        if (_currentUserContext.UserId is not { } userKey)
        {
            return false;
        }

        return await _scopeReader.GetPersonAccountKeyAsync(resourceType, resourceKey, cancellationToken) == userKey;
    }

    private const string OwnSignOff = "Nobody signs off their own probe or badge.";

    /// <summary>
    /// Signing a probe and confirming a badge vouch for someone else's work. A leader is a member
    /// too, and a group-scoped grant always reaches one's own record, so without this a впорядник
    /// or a Звʼязковий would sign their own probe.
    /// </summary>
    private static readonly IReadOnlySet<KurinCapability> SignOffCapabilities = new HashSet<KurinCapability>
    {
        KurinCapability.SignProbe,
        KurinCapability.UnsignProbe,
        KurinCapability.ReviewBadge
    };

    private static bool SignsOff(ResourceType resourceType, ResourceAction action) =>
        KurinCapabilities.All.Any(descriptor => SignOffCapabilities.Contains(descriptor.Key)
                                                && descriptor.Resource == resourceType
                                                && descriptor.Action == action);

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

        if (SignOffCapabilities.Contains(capability) && await IsCallersOwnAsync(scopeResourceType, scopeResourceKey, cancellationToken))
        {
            return ResourceAccessDecision.Deny(OwnSignOff);
        }

        var decision = await CheckScopeAsync(grantedScope.Value, scopeResourceType, scopeResourceKey, cancellationToken);
        var descriptor = KurinCapabilities.All.First(candidate => candidate.Key == capability);
        if (decision.IsAllowed && ChangesYouthRecord(descriptor.Resource, descriptor.Action))
        {
            return await SeniorYouthRecordAsync(descriptor.Resource, grantedScope.Value, scopeResourceType, scopeResourceKey, ownerMayChange: false, cancellationToken)
                   ?? decision;
        }

        return decision;
    }

    public async Task<ResourceAccessDecision> CheckSeniorYouthRecordAsync(
        ResourceType resourceType,
        ResourceAction action,
        Guid memberKey,
        bool ownerMayChange = false,
        CancellationToken cancellationToken = default,
        KurinCapability? capability = null)
    {
        if (Precheck() is { } early)
        {
            return early;
        }

        // A guard on top of the endpoint's own check, not instead of it: it only ever refuses.
        var grantedScope = capability is { } named
            ? (await _permissions.ReachAsync(named, cancellationToken)).ToScope()
            : await _permissions.WidestScopeAsync(resourceType, action, cancellationToken);
        return await SeniorYouthRecordAsync(resourceType, grantedScope ?? AccessScope.Own, ResourceType.Member, memberKey, ownerMayChange, cancellationToken)
               ?? ResourceAccessDecision.Allow("Not a senior's youth record, or a change it allows.");
    }

    private const string SeniorProbe = "A senior's probes are an archive: nobody signs or changes them.";
    private const string SeniorOwnYouthRecord = "A senior does not enter their own youth record.";
    private const string SeniorYouthRecordKurinWide = "A senior's youth record is changed only by who holds the kurin-wide right where they stand now.";

    /// <summary>Probes and вмілості: the youth record the guard covers through ordinary permissions.</summary>
    private static bool ChangesYouthRecord(ResourceType resourceType, ResourceAction action) =>
        action != ResourceAction.Read && resourceType is ResourceType.ProbeProgress or ResourceType.BadgeProgress;

    /// <summary>
    /// The refusal for a change to the youth record of someone who stands in a УСП or УПС kurin, or
    /// null when the change may go ahead. Former kurins never get this far: the scope check already
    /// holds them to the kurin the person stands in.
    /// </summary>
    private async Task<ResourceAccessDecision?> SeniorYouthRecordAsync(
        ResourceType resourceType,
        AccessScope grantedScope,
        ResourceType scopeResourceType,
        Guid scopeResourceKey,
        bool ownerMayChange,
        CancellationToken cancellationToken)
    {
        if (!await _scopeReader.StandsInSeniorKurinAsync(scopeResourceType, scopeResourceKey, cancellationToken))
        {
            return null;
        }

        if (resourceType == ResourceType.ProbeProgress)
        {
            return ResourceAccessDecision.Deny(SeniorProbe);
        }

        if (await IsCallersOwnAsync(scopeResourceType, scopeResourceKey, cancellationToken))
        {
            return ownerMayChange ? null : ResourceAccessDecision.Deny(SeniorOwnYouthRecord);
        }

        return grantedScope == AccessScope.KurinWide ? null : ResourceAccessDecision.Deny(SeniorYouthRecordKurinWide);
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

        var isOwn = _currentUserContext.UserId is { } userKey && scope.MemberUserKey == userKey;
        var senior = await _scopeReader.StandsInSeniorKurinAsync(scopeResourceType, scopeResourceKey, cancellationToken);
        foreach (var descriptor in KurinCapabilities.All)
        {
            if (isOwn && SignOffCapabilities.Contains(descriptor.Key))
            {
                continue;
            }

            var grantedScope = (await _permissions.ReachAsync(descriptor.Key, cancellationToken)).ToScope();
            var seniorRefuses = senior && ChangesYouthRecord(descriptor.Resource, descriptor.Action)
                && (descriptor.Resource == ResourceType.ProbeProgress || isOwn || grantedScope != AccessScope.KurinWide);
            if (grantedScope is not null && !seniorRefuses
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
