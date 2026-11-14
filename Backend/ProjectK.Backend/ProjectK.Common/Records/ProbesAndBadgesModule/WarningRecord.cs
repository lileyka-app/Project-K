using ProjectK.Common.Enums.MemberModule;


namespace ProjectK.Common.Records.ProbesAndBadgesModule;

/// <summary>A пересторога in force: whose, what level, since when.</summary>
public sealed record WarningRecord(Guid MemberKey, MemberWarningLevel Level, DateTime IssuedAtUtc);
