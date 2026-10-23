namespace ProjectK.Common.Models.Dtos.AuthModule;

/// <summary>
/// What an account may do in the kurin it is in, as the clients read it: the same four facts the
/// sign-in response carries, so a refresh can hand a changed office or policy to a session that
/// is already open.
/// </summary>
public sealed class AccessSnapshot
{
    public bool IsAdmin { get; init; }
    public IReadOnlyCollection<string> Roles { get; init; } = [];

    /// <summary>Permission claim values (<c>Resource:Action:Scope</c>), resolved with the kurin's policy.</summary>
    public IReadOnlyCollection<string> Permissions { get; init; } = [];

    public string? KurinKey { get; init; }
}
