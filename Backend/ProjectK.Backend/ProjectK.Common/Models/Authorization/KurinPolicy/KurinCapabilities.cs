using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Authorization.KurinPolicy;

/// <summary>
/// The one table of what a kurin may set for itself. A configurable right is added here and
/// nowhere else: its default lives in code, so a new row needs no data migration, and
/// <see cref="RolePermissionMap"/> stops granting its pair statically on its own.
/// </summary>
public static class KurinCapabilities
{
    private const CapabilityReach K = CapabilityReach.Kurin;
    private const CapabilityReach G = CapabilityReach.OwnGroup;
    private const CapabilityReach N = CapabilityReach.None;

    /// <summary>Every audience, in the order the settings page shows them.</summary>
    public static readonly IReadOnlyList<PolicyAudience> Audiences = Enum.GetValues<PolicyAudience>();

    /// <summary>Every row, in the order the settings page shows them.</summary>
    public static readonly IReadOnlyList<CapabilityDescriptor> All =
    [
        Data(KurinCapability.SeeContacts, ResourceType.MemberContacts, K, K, K, K, K, K, K, K),
        Data(KurinCapability.SeeBirthDate, ResourceType.MemberBirthDate, K, K, K, K, K, K, K, K),
        Data(KurinCapability.SeeBirthday, ResourceType.MemberBirthday, K, K, K, K, K, K, K, K),
        Data(KurinCapability.SeeAddress, ResourceType.MemberAddress, K, G, G, N, N, N, N, N),
        Data(KurinCapability.SeeParents, ResourceType.MemberParents, K, G, G, N, N, N, N, N),
        Data(KurinCapability.SeeCardExtras, ResourceType.MemberExtras, K, G, G, N, N, N, N, N),

        Act(KurinCapability.GiveWarning1, CapabilityGroup.WarningsAwards, ResourceType.MemberWarning, ResourceAction.Create, K, G, G, N, N, N, N),
        Act(KurinCapability.GiveWarning2, CapabilityGroup.WarningsAwards, ResourceType.MemberWarning, ResourceAction.Create, K, G, G, N, N, N, N),
        Act(KurinCapability.GiveWarning3, CapabilityGroup.WarningsAwards, ResourceType.MemberWarning, ResourceAction.Create, K, G, G, N, N, N, N),
        Act(KurinCapability.CancelWarning, CapabilityGroup.WarningsAwards, ResourceType.MemberWarning, ResourceAction.Delete, K, N, N, N, N, N, N),
        Act(KurinCapability.ConfirmAward1, CapabilityGroup.WarningsAwards, ResourceType.MemberAward, ResourceAction.Update, K, G, G, N, N, N, N),
        Act(KurinCapability.ConfirmAward2, CapabilityGroup.WarningsAwards, ResourceType.MemberAward, ResourceAction.Update, K, G, G, N, N, N, N),
        Act(KurinCapability.ConfirmAward3, CapabilityGroup.WarningsAwards, ResourceType.MemberAward, ResourceAction.Update, K, G, G, N, N, N, N),
        Act(KurinCapability.ConfirmAward4, CapabilityGroup.WarningsAwards, ResourceType.MemberAward, ResourceAction.Update, K, G, G, N, N, N, N),
        Act(KurinCapability.RemoveAward, CapabilityGroup.WarningsAwards, ResourceType.MemberAward, ResourceAction.Delete, K, N, N, N, N, N, N),

        Act(KurinCapability.SignProbe, CapabilityGroup.ProbesBadges, ResourceType.ProbeProgress, ResourceAction.Update, K, G, G, N, N, N, N),
        Act(KurinCapability.UnsignProbe, CapabilityGroup.ProbesBadges, ResourceType.ProbeProgress, ResourceAction.Delete, K, G, G, N, N, N, N),
        Act(KurinCapability.ReviewBadge, CapabilityGroup.ProbesBadges, ResourceType.BadgeProgress, ResourceAction.Update, K, G, G, N, N, N, N),

        Act(KurinCapability.ReadJournal, CapabilityGroup.Journal, ResourceType.KurinJournal, ResourceAction.Read, K, K, K, N, N, N, N)
    ];

    private static readonly Dictionary<KurinCapability, CapabilityDescriptor> ByKey =
        All.ToDictionary(descriptor => descriptor.Key);

    private static readonly HashSet<(ResourceType, ResourceAction)> ConfigurablePairs =
        All.Select(descriptor => (descriptor.Resource, descriptor.Action)).ToHashSet();

    /// <summary>The row of a capability.</summary>
    public static CapabilityDescriptor Get(KurinCapability capability) => ByKey[capability];

    /// <summary>The capability of giving a пересторога of <paramref name="level"/>.</summary>
    public static KurinCapability GiveWarning(MemberWarningLevel level) => level switch
    {
        MemberWarningLevel.Level1 => KurinCapability.GiveWarning1,
        MemberWarningLevel.Level2 => KurinCapability.GiveWarning2,
        MemberWarningLevel.Level3 => KurinCapability.GiveWarning3,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown warning level.")
    };

    /// <summary>The capability of confirming a відзначення of <paramref name="level"/>.</summary>
    public static KurinCapability ConfirmAward(MemberAwardLevel level) => level switch
    {
        MemberAwardLevel.First => KurinCapability.ConfirmAward1,
        MemberAwardLevel.Second => KurinCapability.ConfirmAward2,
        MemberAwardLevel.Third => KurinCapability.ConfirmAward3,
        MemberAwardLevel.Fourth => KurinCapability.ConfirmAward4,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown award level.")
    };

    /// <summary>
    /// Whether (resource, action) is decided by the kurin's policy rather than by the static map.
    /// Every scope of such a pair comes from the policy, except the self-service <c>Own</c> grants.
    /// </summary>
    public static bool IsConfigurable(ResourceType resource, ResourceAction action) =>
        ConfigurablePairs.Contains((resource, action));

    private static CapabilityDescriptor Data(
        KurinCapability key,
        ResourceType resource,
        params CapabilityReach[] reaches) =>
        new(key, CapabilityGroup.MemberData, resource, ResourceAction.Read, Defaults(reaches), YouthConfigurable: true);

    // Actions never reach the youth baseline, so their Youth cell is not written out.
    private static CapabilityDescriptor Act(
        KurinCapability key,
        CapabilityGroup group,
        ResourceType resource,
        ResourceAction action,
        params CapabilityReach[] reaches) =>
        new(key, group, resource, action, Defaults([.. reaches, N]), YouthConfigurable: false);

    private static Dictionary<PolicyAudience, CapabilityReach> Defaults(CapabilityReach[] reaches)
    {
        if (reaches.Length != Audiences.Count)
        {
            throw new InvalidOperationException($"A capability needs a default for each of the {Audiences.Count} audiences.");
        }

        return Audiences.Select((audience, index) => (audience, reaches[index]))
            .ToDictionary(pair => pair.audience, pair => pair.Item2);
    }
}
