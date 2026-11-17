using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Services;

/// <summary>
/// Scoped to the request: the roles and kurin come from the token, the policy from
/// <see cref="IKurinAccessPolicyReader"/>, and both are worked out on first use only.
/// </summary>
public sealed class CurrentPermissions : ICurrentPermissions
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IKurinAccessPolicyReader _policies;

    private KurinAccessPolicy? _policy;
    private IReadOnlyCollection<Permission>? _permissions;

    public CurrentPermissions(ICurrentUserContext currentUser, IKurinAccessPolicyReader policies)
    {
        _currentUser = currentUser;
        _policies = policies;
    }

    public async Task<IReadOnlyCollection<Permission>> GetAsync(CancellationToken cancellationToken = default)
    {
        return _permissions ??= RolePermissionRules.Resolve(Roles(), await PolicyAsync(cancellationToken));
    }

    public async Task<AccessScope?> WidestScopeAsync(
        ResourceType resource,
        ResourceAction action,
        CancellationToken cancellationToken = default)
    {
        return RolePermissionRules.WidestScope(await GetAsync(cancellationToken), resource, action);
    }

    public async Task<CapabilityReach> ReachAsync(KurinCapability capability, CancellationToken cancellationToken = default)
    {
        var policy = await PolicyAsync(cancellationToken);
        return policy.Reach(capability, PolicyAudienceRules.For(Roles()));
    }

    private IReadOnlyCollection<string> Roles() => _currentUser.Roles ?? [];

    private async Task<KurinAccessPolicy> PolicyAsync(CancellationToken cancellationToken)
    {
        if (_policy is not null)
        {
            return _policy;
        }

        _policy = _currentUser.KurinKey is { } kurinKey
            ? await _policies.GetAsync(kurinKey, cancellationToken)
            : KurinAccessPolicy.Default;
        return _policy;
    }
}
