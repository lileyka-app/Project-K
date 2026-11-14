namespace ProjectK.Common.Records.ProbesAndBadgesModule;

/// <summary>A whole проба verified for someone of the kurin, and when.</summary>
public sealed record ProbeClosedRecord(Guid MemberKey, string ProbeId, DateTime ClosedAtUtc);
