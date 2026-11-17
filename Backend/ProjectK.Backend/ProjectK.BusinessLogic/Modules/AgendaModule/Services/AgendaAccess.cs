using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AgendaModule.Requests;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Records.AgendaModule;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Rules.AgendaModule;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Services;

public sealed class AgendaAccess : IAgendaAccess
{
    private static readonly KurinCapability[] AssignCapabilities =
    [
        KurinCapability.AssignKurin, KurinCapability.AssignGroups, KurinCapability.AssignMembers, KurinCapability.AssignProvid,
        KurinCapability.AssignTags
    ];

    private readonly ICurrentUserContext _currentUser;
    private readonly IMembershipDirectory _memberships;
    private readonly IResourceScopeReader _scopeReader;
    private readonly IResourceAccessService _resourceAccess;
    private readonly ICurrentPermissions _permissions;
    private readonly IKurinTagDirectory _tags;
    private readonly IOfficeDirectory _offices;

    public AgendaAccess(
        ICurrentUserContext currentUser,
        IUnitOfWork uow,
        IMembershipDirectory memberships,
        IResourceAccessService resourceAccess,
        ICurrentPermissions permissions,
        IKurinTagDirectory tags,
        IOfficeDirectory offices,
        IResourceScopeReader scopeReader)
    {
        _tags = tags;
        _offices = offices;
        _currentUser = currentUser;
        _memberships = memberships;
        _scopeReader = scopeReader;
        _resourceAccess = resourceAccess;
        _permissions = permissions;
    }

    public async Task<AgendaViewerContext> BuildViewerAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var userKey = _currentUser.UserId;

        // Whole-kurin leadership can manage groups anywhere; a гуртковий leader manages only its
        // groups. Both questions are asked through the same extensions every other handler uses.
        var canSeeWholeKurin = _currentUser.CanManageWholeKurin();
        var isLeadership = _currentUser.IsLeadership();

        Guid? memberKey = null;
        Guid? ownGroupKey = null;
        if (userKey.HasValue)
        {
            // The membership in this kurin, not the newest one: someone who stands in two kurins is
            // a member of both, and an item aimed at them or their гурток here must reach them here.
            var member = await _memberships.FindInKurinByAccountAsync(userKey.Value, kurinKey, cancellationToken);
            if (member is not null)
            {
                memberKey = member.MemberKey;
                ownGroupKey = member.GroupKey;
            }
        }

        // Проводи/КВ the viewer belongs to — so an item aimed at their провід is visible to them.
        var leadershipKeys = memberKey.HasValue
            ? await _offices.GetLeadershipKeysForMemberAsync(memberKey.Value, cancellationToken)
            : (IReadOnlyList<Guid>)Array.Empty<Guid>();

        // The tags the viewer carries in this kurin: an item aimed at a tag reaches whoever has it now.
        var tagKeys = memberKey.HasValue
            ? await _tags.GetTagKeysOfMemberAsync(kurinKey, memberKey.Value, cancellationToken)
            : [];

        var visibilityGroups = new HashSet<Guid>();
        if (ownGroupKey.HasValue)
        {
            visibilityGroups.Add(ownGroupKey.Value);
        }

        // A гуртковий answers for tasks set to their гурток even where their office grants no right to
        // manage it (a youth гуртковий of УПЮ), so any group office counts here, not only leadership.
        var holdsGroupOffice = (_currentUser.Roles ?? Array.Empty<string>()).Any(SystemRoleNames.IsGroupOffice);
        IReadOnlyCollection<Guid> ledGroups = Array.Empty<Guid>();
        if ((isLeadership || holdsGroupOffice) && !canSeeWholeKurin && userKey.HasValue)
        {
            ledGroups = await _scopeReader.GetLedGroupKeysAsync(userKey.Value, kurinKey, cancellationToken);
            foreach (var groupKey in ledGroups)
            {
                visibilityGroups.Add(groupKey);
            }
        }

        return new AgendaViewerContext(
            KurinKey: kurinKey,
            ViewerUserKey: userKey,
            ViewerMemberKey: memberKey,
            ViewerOwnGroupKey: ownGroupKey,
            VisibilityGroupKeys: visibilityGroups,
            ViewerLeadershipKeys: leadershipKeys,
            CanSeeWholeKurin: canSeeWholeKurin,
            IsLeadership: isLeadership,
            LedGroupKeysOrNull: ledGroups,
            ViewerTagKeysOrNull: tagKeys,
            TagCarriersReach: await _tags.GetCarrierReachAsync(kurinKey, cancellationToken));
    }

    public async Task<ResourceAccessDecision> AuthorizeTargetAsync(
        AgendaTargetInput target,
        CancellationToken cancellationToken = default)
    {
        // A kurin with tags off takes no new tag targets; those it already has stay (only new ones come here).
        if (target.TargetType == AgendaTargetType.Tag
            && (_currentUser.KurinKey is not { } kurinKey || !await _tags.IsEnabledAsync(kurinKey, cancellationToken)))
        {
            return ResourceAccessDecision.Deny("The kurin has tags turned off.");
        }

        (KurinCapability Capability, ResourceType Scope)? right = target.TargetType switch
        {
            AgendaTargetType.Kurin => (KurinCapability.AssignKurin, ResourceType.Kurin),
            AgendaTargetType.Group => (KurinCapability.AssignGroups, ResourceType.Group),
            AgendaTargetType.Member => (KurinCapability.AssignMembers, ResourceType.Member),
            AgendaTargetType.Leadership => (KurinCapability.AssignProvid, ResourceType.Leadership),
            AgendaTargetType.Tag => (KurinCapability.AssignTags, ResourceType.MemberTag),
            _ => null
        };

        // A провід's scope is its гурток's, or the kurin alone for the КВ and the курінний провід —
        // so an own-group reach covers the провід of a гурток one leads and never a kurin-level one.
        return right is { } known
            ? await _resourceAccess.CheckCapabilityAsync(known.Capability, known.Scope, target.TargetKey, cancellationToken)
            : ResourceAccessDecision.Deny($"Unknown target type {target.TargetType}.");
    }

    public async Task<bool> CanAuthorAsync(CancellationToken cancellationToken = default)
    {
        foreach (var capability in AssignCapabilities)
        {
            if (await _permissions.ReachAsync(capability, cancellationToken) > CapabilityReach.None)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<AgendaAssignReach> GetAssignReachAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var kurin = await _permissions.ReachAsync(KurinCapability.AssignKurin, cancellationToken);
        var groups = await _permissions.ReachAsync(KurinCapability.AssignGroups, cancellationToken);
        var members = await _permissions.ReachAsync(KurinCapability.AssignMembers, cancellationToken);
        var provid = await _permissions.ReachAsync(KurinCapability.AssignProvid, cancellationToken);
        var tags = await _permissions.ReachAsync(KurinCapability.AssignTags, cancellationToken);

        var userKey = _currentUser.UserId;
        IReadOnlySet<Guid> ledGroups = new HashSet<Guid>();
        if (userKey.HasValue && new[] { kurin, groups, members, provid }.Contains(CapabilityReach.OwnGroup))
        {
            ledGroups = (await _scopeReader.GetLedGroupKeysAsync(userKey.Value, kurinKey, cancellationToken)).ToHashSet();
        }

        return new AgendaAssignReach(kurin, groups, members, provid, ledGroups, userKey, tags);
    }

    /// <summary>
    /// Creator or leadership may edit/delete an item. This and the check below are pure functions
    /// over an already-loaded item, shared by the update, delete and status handlers, so no extra
    /// queries run; whether a viewer may move a task is <see cref="AgendaCompletionRules.CanChangeStatus"/>.
    /// </summary>
    public static bool CanManage(AgendaItem item, AgendaViewerContext viewer)
    {
        if (viewer.CanSeeWholeKurin)
        {
            return true;
        }

        if (viewer.ViewerUserKey.HasValue && item.CreatedByUserKey == viewer.ViewerUserKey.Value)
        {
            return true;
        }

        if (viewer.IsLeadership)
        {
            return item.Assignments.Any(a =>
                a.TargetType == AgendaTargetType.Group && viewer.VisibilityGroupKeys.Contains(a.TargetKey));
        }

        return false;
    }

    /// <summary>
    /// Whether the item is visible to the viewer — mirrors the repository's feed filter so an RSVP is
    /// only accepted on events the user could actually see. Requires <see cref="AgendaItem.Assignments"/>.
    /// </summary>
    public static bool IsVisibleTo(AgendaItem item, AgendaViewerContext viewer)
        => AgendaVisibilityRules.IsVisible(item, viewer.ToScope());
}
