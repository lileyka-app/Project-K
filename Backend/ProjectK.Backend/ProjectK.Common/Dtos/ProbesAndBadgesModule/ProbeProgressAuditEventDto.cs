using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

public sealed record ProbeProgressAuditEventDto(
    Guid ProbeProgressAuditEventKey,
    ProbeProgressStatus? FromStatus,
    ProbeProgressStatus ToStatus,
    string Action,
    Guid? ActorUserKey,
    string? ActorName,
    string ActorRole,
    DateTime OccurredAtUtc,
    string? Note);
