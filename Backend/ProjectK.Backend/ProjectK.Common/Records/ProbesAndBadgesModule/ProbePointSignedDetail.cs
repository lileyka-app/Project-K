namespace ProjectK.Common.Records.ProbesAndBadgesModule;

/// <summary>One signed point of a проба, with who signed it and in what capacity.</summary>
public sealed record ProbePointSignedDetail(
    string ProbeId,
    string PointId,
    DateTime? SignedAtUtc,
    string? SignedByName,
    string? SignedByRole);
