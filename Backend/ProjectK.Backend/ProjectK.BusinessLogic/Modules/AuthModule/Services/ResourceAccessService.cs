using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Records.AuthModule;
using ProjectK.Common.Records.InfrastructureModule;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <summary>
/// The single authorization decision point. It answers two orthogonal questions: <b>what</b> the
/// user may do (their <see cref="Permission"/> set, resolved from roles and the kurin's policy by
/// <see cref="ICurrentPermissions"/>) and <b>where</b> it applies (the <see cref="AccessScope"/> of
/// the widest matching permission, checked against the resource's kurin/group/owner by
/// <see cref="ResourceScopeService"/>). On top come two refusals: nobody signs off their own work
/// (<see cref="SignOffRules"/>), and a senior's youth record is guarded by
/// <see cref="SeniorYouthRecordService"/>. Role names are never inspected here beyond the
/// system-admin bypass.
/// </summary>
/// <remarks>
/// The collaborators share this request's user, scope reader and cache, so they are built here
/// rather than injected.
/// </remarks>
public class ResourceAccessService : IResourceAccessService
{
    private readonly IResourceScopeRepository _scopeReader;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ICurrentPermissions _permissions;
    private readonly ResourceScopeService _scopes;
    private readonly SeniorYouthRecordService _seniorYouthRecord;

    public ResourceAccessService(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IBackendCache cache,
        ICurrentPermissions permissions)
    {
        _scopeReader = unitOfWork.ResourceScopes;
        _currentUserContext = currentUserContext;
        _permissions = permissions;
        _scopes = new ResourceScopeService(_scopeReader, currentUserContext, cache);
        _seniorYouthRecord = new SeniorYouthRecordService(_scopeReader, _scopes);
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

        if (SignOffRules.SignsOff(resourceType, action) && await _scopes.IsCallersOwnAsync(scopeResourceType, scopeResourceKey, cancellationToken))
        {
            return ResourceAccessDecision.Deny(SignOffRules.OwnSignOff);
        }

        var decision = await _scopes.CheckAsync(grantedScope.Value, scopeResourceType, scopeResourceKey, cancellationToken);
        if (decision.IsAllowed && SeniorYouthRecordService.ChangesYouthRecord(resourceType, action))
        {
            return await _seniorYouthRecord.RefusalAsync(resourceType, grantedScope.Value, scopeResourceType, scopeResourceKey, ownerMayChange: false, cancellationToken)
                   ?? decision;
        }

        if (decision.IsAllowed || action != ResourceAction.Read)
        {
            return decision;
        }

        // A person released from their kurin, or not yet taken into the next one, stands nowhere —
        // and every scope above is a kurin's. Their own record they still read: the history is
        // theirs, not the kurin's. Reading only; changing it is the kurin's again once they stand in one.
        return await _scopes.IsCallersOwnAsync(scopeResourceType, scopeResourceKey, cancellationToken)
            ? ResourceAccessDecision.Allow("Own record, read outside any kurin scope.")
            : decision;
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

        if (SignOffRules.Capabilities.Contains(capability) && await _scopes.IsCallersOwnAsync(scopeResourceType, scopeResourceKey, cancellationToken))
        {
            return ResourceAccessDecision.Deny(SignOffRules.OwnSignOff);
        }

        var decision = await _scopes.CheckAsync(grantedScope.Value, scopeResourceType, scopeResourceKey, cancellationToken);
        var descriptor = KurinCapabilityRules.All.First(candidate => candidate.Key == capability);
        if (decision.IsAllowed && SeniorYouthRecordService.ChangesYouthRecord(descriptor.Resource, descriptor.Action))
        {
            return await _seniorYouthRecord.RefusalAsync(descriptor.Resource, grantedScope.Value, scopeResourceType, scopeResourceKey, ownerMayChange: false, cancellationToken)
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
        return await _seniorYouthRecord.RefusalAsync(resourceType, grantedScope ?? AccessScope.Own, ResourceType.Member, memberKey, ownerMayChange, cancellationToken)
               ?? ResourceAccessDecision.Allow("Not a senior's youth record, or a change it allows.");
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
                reachable.UnionWith(KurinCapabilityRules.All.Select(descriptor => descriptor.Key));
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
        foreach (var descriptor in KurinCapabilityRules.All)
        {
            if (isOwn && SignOffRules.Capabilities.Contains(descriptor.Key))
            {
                continue;
            }

            var grantedScope = (await _permissions.ReachAsync(descriptor.Key, cancellationToken)).ToScope();
            var seniorRefuses = senior && SeniorYouthRecordService.ChangesYouthRecord(descriptor.Resource, descriptor.Action)
                && (descriptor.Resource == ResourceType.ProbeProgress || isOwn || grantedScope != AccessScope.KurinWide);
            if (grantedScope is not null && !seniorRefuses
                && (await _scopes.EvaluateAsync(grantedScope.Value, scope, currentKurinKey, cancellationToken)).IsAllowed)
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
        if (_currentUserContext.IsInRole(SystemRoleNames.Admin) && _currentUserContext.KurinKey is null)
        {
            return ResourceAccessDecision.Allow("Admin bypass: no kurin scope selected.");
        }

        return null;
    }
}
