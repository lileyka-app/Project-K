using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.Common.Records.IdentityModule;

/// <summary>An office a member currently holds: the провід it belongs to and the role within it.</summary>
public sealed record MemberOffice(LeadershipType Type, LeadershipRole Role);
