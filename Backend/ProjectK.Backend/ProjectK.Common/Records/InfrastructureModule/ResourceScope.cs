namespace ProjectK.Common.Records.InfrastructureModule;

/// <summary>
/// The keys an authorization decision is made from. MemberUserKey is the user a member
/// record belongs to, used for the "own profile" rules.
/// <para>
/// <paramref name="GroupKeys"/> covers resources that reach more than one гурток at once — an agenda
/// item may be assigned to several. A group-scoped grant passes when <paramref name="GroupKey"/> or
/// any entry of <paramref name="GroupKeys"/> is led by the caller.
/// </para>
/// </summary>
public sealed record ResourceScope(
    Guid KurinKey,
    Guid? GroupKey,
    Guid? MemberUserKey,
    IReadOnlyCollection<Guid>? GroupKeys = null);
