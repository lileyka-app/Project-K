using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Records.IdentityModule;

/// <summary>Lightweight reference to a провід/КВ: its key, провід type and (for a гуртковий провід) its group.</summary>
public sealed record LeadershipRef(Guid LeadershipKey, LeadershipType Type, Guid? GroupKey);
