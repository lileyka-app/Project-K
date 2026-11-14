namespace ProjectK.Common.Records.ProbesAndBadgesModule;

/// <summary>A вмілість confirmed to someone of the kurin, and when.</summary>
public sealed record BadgeConfirmedRecord(Guid MemberKey, string BadgeId, DateTime ConfirmedAtUtc);
