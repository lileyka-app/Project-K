using ProjectK.Common.Enums.MemberModule;


namespace ProjectK.Common.Records.KurinModule;

/// <summary>A membership that has ended, with the гуртки it passed through.</summary>
public sealed record ArchivedMembership(
    Guid MembershipKey,
    Guid MemberKey,
    Guid? UserKey,
    MembershipKind Kind,
    DateTime JoinedAtUtc,
    DateTime LeftAtUtc,
    IReadOnlyList<ArchivedStint> Stints);
