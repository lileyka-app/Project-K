using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Records.AuthModule;

/// <summary>An office a member currently holds: the провід it belongs to and the role within it.</summary>
public sealed record MemberOffice(LeadershipType Type, LeadershipRole Role);
