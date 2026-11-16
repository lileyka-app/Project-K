using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Services;

/// <summary>
/// How far the current user may assign agenda items in one kurin, as the kurin's policy says: one
/// reach per kind of target, and the гуртки an own-group reach covers. Answers the same question as
/// <see cref="IAgendaAccess.AuthorizeTargetAsync"/> for many targets at once, without a read per target.
/// </summary>
/// <param name="Kurin">The reach of <see cref="KurinCapability.AssignKurin"/>.</param>
/// <param name="Groups">The reach of <see cref="KurinCapability.AssignGroups"/>.</param>
/// <param name="Members">The reach of <see cref="KurinCapability.AssignMembers"/>.</param>
/// <param name="Provid">The reach of <see cref="KurinCapability.AssignProvid"/>.</param>
/// <param name="Tags">The reach of <see cref="KurinCapability.AssignTags"/>: the kurin or nobody.</param>
/// <param name="LedGroupKeys">The гуртки the user leads, their own member гурток included; empty unless some reach is own-group.</param>
/// <param name="UserKey">The user's account, which an own-group reach always covers.</param>
public sealed record AgendaAssignReach(
    CapabilityReach Kurin,
    CapabilityReach Groups,
    CapabilityReach Members,
    CapabilityReach Provid,
    IReadOnlySet<Guid> LedGroupKeys,
    Guid? UserKey,
    CapabilityReach Tags = CapabilityReach.None)
{
    /// <summary>Whether the user may author an item at all: some kind of target is within reach.</summary>
    public bool CanAuthor => Kurin > CapabilityReach.None || Groups > CapabilityReach.None
        || Members > CapabilityReach.None || Provid > CapabilityReach.None || Tags > CapabilityReach.None;

    /// <summary>Whether a tag may be a target: tags are the kurin's, so only a kurin-wide reach covers them.</summary>
    public bool ReachesTags => Tags == CapabilityReach.Kurin;

    /// <summary>Whether the whole kurin may be a target.</summary>
    public bool ReachesKurin => Kurin == CapabilityReach.Kurin;

    /// <summary>Whether <paramref name="groupKey"/> may be a target.</summary>
    public bool ReachesGroup(Guid groupKey) => Covers(Groups, groupKey, null);

    /// <summary>Whether a person seated in <paramref name="groupKey"/> with <paramref name="memberUserKey"/> may be a target.</summary>
    public bool ReachesMember(Guid? groupKey, Guid? memberUserKey) => Covers(Members, groupKey, memberUserKey);

    /// <summary>Whether the провід of <paramref name="groupKey"/> may be a target; null for the КВ and the курінний провід.</summary>
    public bool ReachesProvid(Guid? groupKey) => Covers(Provid, groupKey, null);

    // The same rule ResourceAccessService applies to an own-group grant: one's own record, or a
    // resource in a гурток one leads.
    private bool Covers(CapabilityReach reach, Guid? groupKey, Guid? memberUserKey) => reach switch
    {
        CapabilityReach.Kurin => true,
        CapabilityReach.OwnGroup => (memberUserKey.HasValue && memberUserKey == UserKey)
            || (groupKey.HasValue && LedGroupKeys.Contains(groupKey.Value)),
        _ => false
    };
}
