namespace ProjectK.Common.Records.ProbesAndBadgesModule;

/// <summary>A point of a проба signed off for someone of the kurin, and when.</summary>
public sealed record ProbePointSignedRecord(Guid MemberKey, string ProbeId, string PointId, DateTime SignedAtUtc);
