using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

public sealed record BadgeProgressAuditEventDto(
    Guid BadgeProgressAuditEventKey,
    BadgeProgressStatus? FromStatus,
    BadgeProgressStatus ToStatus,
    string Action,
    Guid? ActorUserKey,
    string? ActorName,
    string ActorRole,
    DateTime OccurredAtUtc,
    string? Note);
