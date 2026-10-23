using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <inheritdoc />
public sealed class AccessSnapshotFactory : IAccessSnapshotFactory
{
    private readonly IPermissionResolver _permissions;

    public AccessSnapshotFactory(IPermissionResolver permissions)
    {
        _permissions = permissions;
    }

    public async Task<AccessSnapshot> CreateAsync(AccessContext access, CancellationToken cancellationToken = default)
    {
        // The kurin's own policy, so the client hides exactly what the server would refuse.
        var permissions = await _permissions.ResolveAsync(access.Roles, access.KurinKey, cancellationToken);

        return new AccessSnapshot
        {
            IsAdmin = access.IsAdmin,
            Roles = [.. access.Roles],
            Permissions = permissions.Select(permission => permission.ToClaimValue()).ToArray(),
            KurinKey = access.KurinKey?.ToString()
        };
    }
}
