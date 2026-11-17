namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

public sealed record ProbePointProgressDto(
    Guid? ProbePointProgressKey,
    string PointId,
    bool IsSigned,
    DateTime? SignedAtUtc,
    Guid? SignedByUserKey,
    string? SignedByName,
    string? SignedByRole);
