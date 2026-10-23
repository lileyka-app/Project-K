using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Services.Audit;

/// <summary>Who did it, as a trail or the kurin's journal will remember them.</summary>
/// <param name="UserKey">The account; null when nobody is signed in.</param>
/// <param name="Name">The name a person will read.</param>
/// <param name="Role">The office that authorised the action.</param>
public sealed record AuditActor(Guid? UserKey, string? Name, string Role);

/// <summary>
/// The actor behind the current request, worked out once and kept for the rest of it: every
/// signature, trail row and journal entry of one request names the same person the same way.
/// </summary>
public sealed class AuditActorResolver
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IMemberDirectory _members;

    private AuditActor? _actor;

    public AuditActorResolver(ICurrentUserContext currentUserContext, IMemberDirectory members)
    {
        _currentUserContext = currentUserContext;
        _members = members;
    }

    /// <summary>
    /// The actor's key, the name a person will read in the trail, and the office that authorised
    /// the action.
    /// <para>
    /// The name is looked up rather than taken from the token, which carries only <c>sub</c>, the
    /// address and the roles. It used to be <c>UserId.ToString()</c> — so every signature and every
    /// вмілість event recorded a guid where a name belongs, and the screens dutifully printed it.
    /// </para>
    /// </summary>
    public async Task<AuditActor> ResolveAsync(CancellationToken cancellationToken)
    {
        if (_actor is not null)
        {
            return _actor;
        }

        var userKey = _currentUserContext.UserId;
        _actor = new AuditActor(
            userKey,
            await ResolveNameAsync(userKey, cancellationToken),
            ResolveRole());
        return _actor;
    }

    /// <summary>
    /// The person's name, or a marker saying whose record could not be named. The markers are
    /// deliberate and readable as markers — a bare guid in the same slot reads as a name that went
    /// wrong, which is exactly the confusion this used to cause.
    /// </summary>
    private async Task<string?> ResolveNameAsync(Guid? actorUserKey, CancellationToken cancellationToken)
    {
        if (actorUserKey is null)
        {
            return null;
        }

        var actor = await _members.FindByAccountAsync(actorUserKey.Value, cancellationToken);
        if (actor is null)
        {
            return $"user:{actorUserKey.Value}";
        }

        return string.IsNullOrWhiteSpace(actor.FullName)
            ? $"member:{actor.MemberKey} / user:{actorUserKey.Value}"
            : actor.FullName;
    }

    /// <summary>
    /// The office recorded: admin, otherwise the one that actually authorised the action — the
    /// widest <c>Member:Update</c> scope among the offices held — and the bare baseline when none
    /// does. Ties break by name so the same user is always recorded the same way; this used to take
    /// whichever office the identity store happened to return first.
    /// </summary>
    private string ResolveRole()
    {
        if (_currentUserContext.IsInRole(SystemRole.Admin))
        {
            return SystemRole.Admin;
        }

        var office = (_currentUserContext.Roles ?? [])
            .Where(role => !string.Equals(role, SystemRole.Member, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(role => RolePermissionMap.WidestScope(
                RolePermissionMap.Resolve(new[] { role }),
                ResourceType.Member,
                ResourceAction.Update) ?? default)
            .ThenBy(role => role, StringComparer.Ordinal)
            .FirstOrDefault();

        return office ?? SystemRole.Member;
    }
}
