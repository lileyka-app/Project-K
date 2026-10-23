using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Notifications;

/// <summary>
/// Tells exactly the people who could act: every office holder in the kurin whose reach covers the
/// member's гурток. For awards and badges that reach is the kurin's policy — a kurin that took
/// confirming away from впорядники stops notifying them too; for a profile it is the static
/// <c>Member:Update</c>. Derived, never listed, because a hardcoded list here kept notifying
/// Курінний and Гуртковий after the офіс model took their member rights away.
/// </summary>
public sealed class ReviewNotificationRecipientResolver : IReviewNotificationRecipientResolver
{
    private static readonly KurinCapability[] AwardCapabilities =
    [
        KurinCapability.ConfirmAward1, KurinCapability.ConfirmAward2,
        KurinCapability.ConfirmAward3, KurinCapability.ConfirmAward4
    ];

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly IKurinAccessPolicyProvider _policies;

    public ReviewNotificationRecipientResolver(
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        IKurinAccessPolicyProvider policies)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _policies = policies;
    }

    public async Task<IReadOnlyCollection<Guid>> ResolveAsync(
        ReviewSubject subject,
        Guid kurinKey,
        Guid? groupKey,
        Guid? excludedUserKey,
        CancellationToken cancellationToken = default)
    {
        var policy = await _policies.GetAsync(kurinKey, cancellationToken);
        var reviewers = new HashSet<Guid>();

        await AddKurinOfficeHoldersAsync(reviewers, subject, policy, kurinKey, groupKey, cancellationToken);
        await AddGroupOfficeHoldersAsync(reviewers, subject, policy, kurinKey, groupKey, cancellationToken);

        if (excludedUserKey.HasValue)
        {
            reviewers.Remove(excludedUserKey.Value);
        }

        return reviewers.ToList();
    }

    /// <summary>
    /// The КВ and the курінний провід: everyone whose office reaches the whole kurin, and those
    /// reaching their own гуртки when they lead this one — which, for an office outside a гурток,
    /// is a mentor assignment.
    /// </summary>
    private async Task AddKurinOfficeHoldersAsync(
        HashSet<Guid> reviewers,
        ReviewSubject subject,
        KurinAccessPolicy policy,
        Guid kurinKey,
        Guid? groupKey,
        CancellationToken cancellationToken)
    {
        var holders = (await _unitOfWork.Leaderships.GetOfficeMembersLookupAsync(kurinKey, LeadershipType.KV, cancellationToken))
            .Concat(await _unitOfWork.Leaderships.GetOfficeMembersLookupAsync(kurinKey, LeadershipType.Kurin, cancellationToken))
            .Where(holder => holder.UserKey.HasValue && holder.UserRole is not null)
            .Select(holder => (UserKey: holder.UserKey!.Value, Reach: ReachOf(subject, policy, holder.UserRole!)))
            .ToList();

        reviewers.UnionWith(holders.Where(holder => holder.Reach == CapabilityReach.Kurin).Select(holder => holder.UserKey));

        var groupReach = holders.Where(holder => holder.Reach == CapabilityReach.OwnGroup).Select(holder => holder.UserKey).ToHashSet();
        if (groupKey is null || groupReach.Count == 0)
        {
            return;
        }

        var mentors = await _unitOfWork.MentorAssignments.GetByGroupKeyAsync(groupKey.Value, cancellationToken);
        reviewers.UnionWith(mentors
            .Where(assignment => assignment.RevokedAtUtc is null && groupReach.Contains(assignment.MentorUserKey))
            .Select(assignment => assignment.MentorUserKey));
    }

    /// <summary>
    /// Гуртковий проводи: of this гурток when their office reaches their own, of every гурток when a
    /// kurin has stretched it to the whole kurin.
    /// </summary>
    private async Task AddGroupOfficeHoldersAsync(
        HashSet<Guid> reviewers,
        ReviewSubject subject,
        KurinAccessPolicy policy,
        Guid kurinKey,
        Guid? groupKey,
        CancellationToken cancellationToken)
    {
        var offices = LeadershipOffices.Grouping[LeadershipType.Group]
            .Select(office => (Office: office, Reach: ReachOf(subject, policy, SystemRole.ForOffice(LeadershipType.Group, office))))
            .ToList();
        var wholeKurin = offices.Where(o => o.Reach == CapabilityReach.Kurin).Select(o => o.Office).ToList();
        var ownGroup = offices.Where(o => o.Reach == CapabilityReach.OwnGroup).Select(o => o.Office).ToList();

        var memberKeys = new List<Guid>();
        if (wholeKurin.Count > 0)
        {
            foreach (var group in await _unitOfWork.Groups.GetAllAsync(kurinKey, cancellationToken))
            {
                memberKeys.AddRange(await _unitOfWork.Leaderships.GetActiveOfficeMemberKeysAsync(
                    wholeKurin, groupKey: group.GroupKey, cancellationToken: cancellationToken));
            }
        }

        if (groupKey.HasValue && ownGroup.Count > 0)
        {
            memberKeys.AddRange(await _unitOfWork.Leaderships.GetActiveOfficeMemberKeysAsync(
                ownGroup, groupKey: groupKey, cancellationToken: cancellationToken));
        }

        if (memberKeys.Count == 0)
        {
            return;
        }

        var memberToUser = (await _members.GetLookupByKurinAsync(kurinKey, cancellationToken))
            .Where(candidate => candidate.UserKey.HasValue)
            .GroupBy(candidate => candidate.MemberKey)
            .ToDictionary(group => group.Key, group => group.First().UserKey!.Value);

        reviewers.UnionWith(memberKeys.Where(memberToUser.ContainsKey).Select(memberKey => memberToUser[memberKey]));
    }

    private static CapabilityReach ReachOf(ReviewSubject subject, KurinAccessPolicy policy, string role)
    {
        if (subject == ReviewSubject.Profile)
        {
            return RolePermissionMap.WidestScope(RolePermissionMap.StaticGrants([role]), ResourceType.Member, ResourceAction.Update) switch
            {
                AccessScope.KurinWide => CapabilityReach.Kurin,
                AccessScope.OwnGroups => CapabilityReach.OwnGroup,
                _ => CapabilityReach.None
            };
        }

        var audiences = PolicyAudiences.For([role]);
        return subject == ReviewSubject.Award
            ? AwardCapabilities.Max(capability => policy.Reach(capability, audiences))
            : policy.Reach(KurinCapability.ReviewBadge, audiences);
    }
}
