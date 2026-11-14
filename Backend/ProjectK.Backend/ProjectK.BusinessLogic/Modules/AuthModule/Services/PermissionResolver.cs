using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Records.AuthModule;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

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

        return RolePermissionMap.Resolve(roles, policy);
    }
}
