namespace ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

public sealed record ProbePointProgressDto(
    Guid? ProbePointProgressKey,
    string PointId,
    bool IsSigned,
    DateTime? SignedAtUtc,
    Guid? SignedByUserKey,
    string? SignedByName,
    string? SignedByRole);
