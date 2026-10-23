using ProjectK.Common.Models.Authorization;

namespace ProjectK.Common.Interfaces.Modules.AuthModule;

/// <summary>
/// What a set of roles may do in one kurin: the static grants of <see cref="RolePermissionMap"/>
/// plus what that kurin's «Права й видимість» gives them. Anything that needs permissions for a
/// kurin other than the request's own — sign-in, the duties of every kurin a person serves —
/// asks here; the request's own kurin is <see cref="ICurrentPermissions"/>.
/// </summary>
public interface IPermissionResolver
{
    /// <summary>
    /// The permissions <paramref name="roles"/> add up to in <paramref name="kurinKey"/>. Without a
    /// kurin there is no policy to read, so the defaults apply.
    /// </summary>
    Task<IReadOnlyCollection<Permission>> ResolveAsync(
        IEnumerable<string> roles,
        Guid? kurinKey,
        CancellationToken cancellationToken = default);
}
