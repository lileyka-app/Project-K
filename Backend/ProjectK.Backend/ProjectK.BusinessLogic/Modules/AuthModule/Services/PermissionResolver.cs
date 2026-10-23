using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <inheritdoc />
public sealed class PermissionResolver : IPermissionResolver
{
    private readonly IKurinAccessPolicyProvider _policies;

    public PermissionResolver(IKurinAccessPolicyProvider policies)
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

        return RolePermissionMap.Resolve(roles, policy);
    }
}
