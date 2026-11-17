using ProjectK.Common.Enums.IdentityModule;

namespace ProjectK.Common.Records.AgendaModule;

/// <summary>
/// Who the current user is on the agenda: their linked member/group, the groups whose items they may
/// see, and whether they manage the whole kurin. Built once per request from the auth claims and the
/// member record.
/// </summary>
public sealed record AgendaViewerContext(
    Guid KurinKey,
    Guid? ViewerUserKey,
    Guid? ViewerMemberKey,
    Guid? ViewerOwnGroupKey,
    IReadOnlyCollection<Guid> VisibilityGroupKeys,
    IReadOnlyCollection<Guid> ViewerLeadershipKeys,
    bool CanSeeWholeKurin,
    bool IsLeadership,
    IReadOnlyCollection<Guid>? LedGroupKeysOrNull = null,
    IReadOnlyCollection<Guid>? ViewerTagKeysOrNull = null,
    CapabilityReach TagCarriersReach = CapabilityReach.None)
{
    /// <summary>
    /// Whether the viewer may see that a person in <paramref name="groupKey"/> carries a tag: by the
    /// kurin's «Бачити теги людей», and always for themselves.
    /// </summary>
    public bool SeesTagCarrier(Guid memberKey, Guid? groupKey) =>
        memberKey == ViewerMemberKey
        || TagCarriersReach == CapabilityReach.Kurin
        || (TagCarriersReach == CapabilityReach.OwnGroup && groupKey is { } g && VisibilityGroupKeys.Contains(g));

    /// <summary>The kurin's tags the viewer carries now; whoever gets a tag later sees what was assigned to it.</summary>
    public IReadOnlyCollection<Guid> ViewerTagKeys => ViewerTagKeysOrNull ?? Array.Empty<Guid>();

    /// <summary>The гуртки the viewer runs as провід or впорядник — narrower than what they see, which also holds their own гурток.</summary>
    public IReadOnlyCollection<Guid> LedGroupKeys => LedGroupKeysOrNull ?? Array.Empty<Guid>();

    public AgendaViewerScope ToScope() =>
        new(KurinKey, ViewerUserKey, ViewerMemberKey, VisibilityGroupKeys, ViewerLeadershipKeys, CanSeeWholeKurin, ViewerTagKeys);
}
