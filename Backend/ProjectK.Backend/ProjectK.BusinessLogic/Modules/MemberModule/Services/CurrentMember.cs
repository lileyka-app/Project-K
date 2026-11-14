using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Services;

/// <inheritdoc />
/// <remarks>Scoped: the cache lives as long as the request.</remarks>
public sealed class CurrentMember : ICurrentMember
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IMemberDirectory _members;
    private readonly IMembershipDirectory _memberships;

    private bool _personRead;
    private MemberSummary? _person;
    private AuditActor? _actor;

    public CurrentMember(ICurrentUserContext currentUser, IMemberDirectory members, IMembershipDirectory memberships)
    {
        _currentUser = currentUser;
        _members = members;
        _memberships = memberships;
    }

    public Guid? UserKey => _currentUser.UserId;

    public async Task<MemberSummary?> FindAsync(CancellationToken cancellationToken = default)
    {
        if (_personRead)
        {
            return _person;
        }

        _person = UserKey is { } userKey
            ? await _members.FindByAccountAsync(userKey, cancellationToken)
            : null;
        _personRead = true;
        return _person;
    }

    public async Task<Guid?> MemberKeyAsync(CancellationToken cancellationToken = default)
        => UserKey is { } userKey && userKey != Guid.Empty
            ? (await FindAsync(cancellationToken))?.MemberKey
            : null;

    public async Task<CurrentPerson?> ReadPersonAsync(CancellationToken cancellationToken = default)
    {
        if (UserKey is not { } userKey || await FindAsync(cancellationToken) is not { } person)
        {
            return null;
        }

        var levels = (await _members.GetLevelsAsync(person.MemberKey, cancellationToken)).ToList();
        var memberships = (await _memberships.GetCurrentForAccountAsync(userKey, cancellationToken))
            .OrderBy(m => m.KurinNumber)
            .ToList();

        return new CurrentPerson(userKey, person, levels, memberships);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The name is looked up rather than taken from the token, which carries only <c>sub</c>, the
    /// address and the roles. It used to be <c>UserId.ToString()</c> — so every signature and every
    /// вмілість event recorded a guid where a name belongs, and the screens dutifully printed it.
    /// </remarks>
    public async Task<AuditActor> ActorAsync(CancellationToken cancellationToken = default)
    {
        if (_actor is not null)
        {
            return _actor;
        }

        _actor = new AuditActor(UserKey, await ActorNameAsync(cancellationToken), ActorRole());
        return _actor;
    }

    /// <summary>
    /// The person's name, or a marker saying whose record could not be named. The markers are
    /// deliberate and readable as markers — a bare guid in the same slot reads as a name that went
    /// wrong, which is exactly the confusion this used to cause.
    /// </summary>
    private async Task<string?> ActorNameAsync(CancellationToken cancellationToken)
    {
        if (UserKey is not { } userKey)
        {
            return null;
        }

        var actor = await FindAsync(cancellationToken);
        if (actor is null)
        {
            return $"user:{userKey}";
        }

        return string.IsNullOrWhiteSpace(actor.FullName)
            ? $"member:{actor.MemberKey} / user:{userKey}"
            : actor.FullName;
    }

    /// <summary>
    /// The office recorded: admin, otherwise the one that actually authorised the action — the
    /// widest <c>Member:Update</c> scope among the offices held — and the bare baseline when none
    /// does. Ties break by name so the same user is always recorded the same way; this used to take
    /// whichever office the identity store happened to return first.
    /// </summary>
    private string ActorRole()
    {
        if (_currentUser.IsInRole(SystemRoleNames.Admin))
        {
            return SystemRoleNames.Admin;
        }

        var office = (_currentUser.Roles ?? [])
            .Where(role => !string.Equals(role, SystemRoleNames.Member, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(role => RolePermissionRules.WidestScope(
                RolePermissionRules.Resolve(new[] { role }),
                ResourceType.Member,
                ResourceAction.Update) ?? default)
            .ThenBy(role => role, StringComparer.Ordinal)
            .FirstOrDefault();

        return office ?? SystemRoleNames.Member;
    }
}
