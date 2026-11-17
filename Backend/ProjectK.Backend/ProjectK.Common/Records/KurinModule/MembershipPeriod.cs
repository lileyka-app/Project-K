using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// One membership as the archive corrects or deletes it: whose it is, its гурток now, its kind and
/// when it ran. A null end is a membership still running.
/// </summary>
public sealed record MembershipPeriod(
    Guid MembershipKey,
    Guid MemberKey,
    Guid? GroupKey,
    MembershipKind Kind,
    DateTime JoinedAtUtc,
    DateTime? LeftAtUtc);
