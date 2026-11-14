namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// Whether a member exists and which account it is linked to. The account flow needs both before it
/// writes anything, and neither is worth loading the member's whole graph for.
/// </summary>
public sealed record MemberAccountLink(Guid MemberKey, Guid? UserKey);
