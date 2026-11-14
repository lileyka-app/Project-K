using ProjectK.Common.Records.AuthModule;

namespace ProjectK.Common.Enums.AuthModule;

/// <summary>
/// The breadth at which a <see cref="Permission"/> applies. This is the "where" axis of
/// authorization, orthogonal to the "what" (resource + action): a role may manage groups
/// across the whole kurin, only within the groups it leads, or only records it owns.
/// Wider tiers subsume narrower ones (<see cref="KurinWide"/> beats <see cref="OwnGroups"/>
/// beats <see cref="Own"/>).
/// </summary>
public enum AccessScope
{
    /// <summary>Only resources the current user personally owns (own member profile/progress).</summary>
    Own,

    /// <summary>Only resources inside the groups the user leads.</summary>
    OwnGroups,

    /// <summary>Any resource inside the user's kurin.</summary>
    KurinWide
}
