using ProjectK.Common.Enums.Shared;

namespace ProjectK.Common.Records.MemberModule;

/// <summary>A senior ступінь a person entered for themselves that still waits for the Звʼязковий, as stored.</summary>
public sealed record WaitingLevel(Guid MemberKey, Guid PlastLevelHistoryKey, PlastLevel PlastLevel, DateOnly DateAchieved);
