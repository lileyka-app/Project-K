using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Records.MemberModule;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// One membership of a kurin as another module needs to see it: whose it is, which гурток, what kind,
/// and for how long. Answered for the whole kurin, past memberships included.
/// </summary>
public sealed record KurinMembershipRecord(
    Guid MembershipKey,
    Guid MemberKey,
    Guid? GroupKey,
    MembershipKind Kind,
    DateTime JoinedAtUtc,
    DateTime? LeftAtUtc);
