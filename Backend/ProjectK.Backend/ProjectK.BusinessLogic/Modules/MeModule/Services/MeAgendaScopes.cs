using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Services;

/// <summary>
/// One kurin the person stands in: how the agenda there sees them, the roles their offices there
/// amount to, and the гуртки they lead there.
/// </summary>
public sealed record MeKurinContext(
    MembershipRecord Membership,
    AgendaViewerContext Viewer,
    IReadOnlyList<string> Roles,
    IReadOnlySet<Guid> LedGroupKeys);

/// <summary>
/// How the person is seen by the agenda of every kurin they currently stand in. The token carries the
/// rights of one kurin only, so the others are worked out here from the offices the person holds
/// there — the same offices the token would be minted from after a switch. Read, never granted.
/// </summary>
public sealed class MeAgendaScopes
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IMembershipDirectory _memberships;
    private readonly IMemberDirectory _members;
    private readonly IOfficeDirectory _offices;
    private readonly IResourceScopeReader _scopeReader;
    private readonly IUnitOfWork _unitOfWork;

    public MeAgendaScopes(
        ICurrentUserContext currentUser,
        IMembershipDirectory memberships,
        IMemberDirectory members,
        IOfficeDirectory offices,
        IResourceScopeReader scopeReader,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _memberships = memberships;
        _members = members;
        _offices = offices;
        _scopeReader = scopeReader;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Empty when nobody is signed in or no person stands behind the account.</summary>
    public Task<IReadOnlyList<MeKurinContext>> BuildAsync(CancellationToken cancellationToken) =>
        _currentUser.UserId is { } userKey
            ? BuildForAsync(userKey, cancellationToken)
            : Task.FromResult<IReadOnlyList<MeKurinContext>>([]);

    /// <summary>
    /// The same for an account named by key rather than by the request's token — the calendar link,
    /// fetched by a calendar app that signs nobody in. Empty when no person stands behind it.
    /// </summary>
    public async Task<IReadOnlyList<MeKurinContext>> BuildForAsync(Guid userKey, CancellationToken cancellationToken)
    {
        var person = await _members.FindByAccountAsync(userKey, cancellationToken);
        if (person is null)
        {
            return [];
        }

        var leadershipKeys = await _unitOfWork.Leaderships.GetActiveLeadershipKeysForMemberAsync(person.MemberKey, cancellationToken);
        var contexts = new List<MeKurinContext>();

        foreach (var membership in (await _memberships.GetCurrentForAccountAsync(userKey, cancellationToken)).OrderBy(m => m.KurinNumber))
        {
            var offices = await _offices.GetForAccountInKurinAsync(userKey, membership.KurinKey, cancellationToken);
            var roles = offices.Select(o => SystemRole.ForOffice(o.Type, o.Role)).ToList();
            var canSeeWholeKurin = RolePermissionMap.GrantsWholeKurinManagement(roles);

            var led = roles.Count > 0 && !canSeeWholeKurin
                ? (await _scopeReader.GetLedGroupKeysAsync(userKey, membership.KurinKey, cancellationToken)).ToHashSet()
                : new HashSet<Guid>();

            var groups = new HashSet<Guid>(led);
            if (membership.GroupKey is { } ownGroup)
            {
                groups.Add(ownGroup);
            }

            contexts.Add(new MeKurinContext(membership, new AgendaViewerContext(
                KurinKey: membership.KurinKey,
                ViewerUserKey: userKey,
                ViewerMemberKey: person.MemberKey,
                ViewerOwnGroupKey: membership.GroupKey,
                VisibilityGroupKeys: groups,
                ViewerLeadershipKeys: leadershipKeys,
                CanSeeWholeKurin: canSeeWholeKurin,
                IsLeadership: roles.Count > 0,
                LedGroupKeysOrNull: led), roles, led));
        }

        return contexts;
    }
}
