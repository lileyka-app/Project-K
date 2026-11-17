using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Services;

/// <inheritdoc />
public sealed class PermissionResolver : IPermissionResolver
{
    private readonly IKurinAccessPolicyReader _policies;

    public PermissionResolver(IKurinAccessPolicyReader policies)
    {
        _policies = policies;
    }

    public async Task<IReadOnlyCollection<Permission>> ResolveAsync(
        IEnumerable<string> roles,
        Guid? kurinKey,
        CancellationToken cancellationToken = default)
    {
        var policy = kurinKey is { } key
            ? await _policies.GetAsync(key, cancellationToken)
            : KurinAccessPolicy.Default;

        return RolePermissionRules.Resolve(roles, policy);
    }
}
