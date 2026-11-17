using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Records.AgendaModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Services;

/// <inheritdoc />
/// <remarks>
/// The token carries the rights of one kurin only, so the others are worked out here from the
/// offices the person holds there — the same offices the token would be minted from after a switch.
/// Read, never granted.
/// </remarks>
public sealed class AgendaViewerDirectory : IAgendaViewerDirectory
{
    private readonly ICurrentMember _currentMember;
    private readonly IMembershipDirectory _memberships;
    private readonly IMemberDirectory _members;
    private readonly IOfficeDirectory _offices;
    private readonly IResourceScopeReader _scopeReader;
    private readonly IKurinTagDirectory _tags;

    public AgendaViewerDirectory(
        ICurrentMember currentMember,
        IMembershipDirectory memberships,
        IMemberDirectory members,
        IOfficeDirectory offices,
        IKurinTagDirectory tags,
        IResourceScopeReader scopeReader)
    {
        _currentMember = currentMember;
        _memberships = memberships;
        _members = members;
        _offices = offices;
        _scopeReader = scopeReader;
        _tags = tags;
    }

    public async Task<IReadOnlyList<AgendaKurinViewer>> GetForCurrentAccountAsync(CancellationToken cancellationToken = default) =>
        _currentMember.UserKey is { } userKey
            ? await BuildAsync(userKey, await _currentMember.FindAsync(cancellationToken), cancellationToken)
            : [];

    public async Task<IReadOnlyList<AgendaKurinViewer>> GetForAccountAsync(Guid userKey, CancellationToken cancellationToken = default)
        => await BuildAsync(userKey, await _members.FindByAccountAsync(userKey, cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<AgendaKurinViewer>> BuildAsync(Guid userKey, MemberSummary? person, CancellationToken cancellationToken)
    {
        if (person is null)
        {
            return [];
        }

        var leadershipKeys = await _offices.GetLeadershipKeysForMemberAsync(person.MemberKey, cancellationToken);
        var contexts = new List<AgendaKurinViewer>();

        foreach (var membership in (await _memberships.GetCurrentForAccountAsync(userKey, cancellationToken)).OrderBy(m => m.KurinNumber))
        {
            var offices = await _offices.GetForAccountInKurinAsync(userKey, membership.KurinKey, cancellationToken);
            var roles = offices.Select(o => SystemRoleNames.ForOffice(o.Type, o.Role)).ToList();
            var canSeeWholeKurin = RolePermissionRules.GrantsWholeKurinManagement(roles);

            var led = roles.Count > 0 && !canSeeWholeKurin
                ? (await _scopeReader.GetLedGroupKeysAsync(userKey, membership.KurinKey, cancellationToken)).ToHashSet()
                : new HashSet<Guid>();

            // An item aimed at a tag reaches whoever carries it in that kurin now.
            var tagKeys = await _tags.GetTagKeysOfMemberAsync(membership.KurinKey, person.MemberKey, cancellationToken);

            var groups = new HashSet<Guid>(led);
            if (membership.GroupKey is { } ownGroup)
            {
                groups.Add(ownGroup);
            }

            contexts.Add(new AgendaKurinViewer(membership, new AgendaViewerContext(
                KurinKey: membership.KurinKey,
                ViewerUserKey: userKey,
                ViewerMemberKey: person.MemberKey,
                ViewerOwnGroupKey: membership.GroupKey,
                VisibilityGroupKeys: groups,
                ViewerLeadershipKeys: leadershipKeys,
                CanSeeWholeKurin: canSeeWholeKurin,
                IsLeadership: roles.Count > 0,
                LedGroupKeysOrNull: led,
                ViewerTagKeysOrNull: tagKeys), roles, led));
        }

        return contexts;
    }
}
