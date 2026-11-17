using ProjectK.Common.Enums.Shared;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>A ступінь a person on a roster reached, with the date, as stored.</summary>
public sealed record MemberListLevel(
    Guid PlastLevelHistoryKey,
    Guid MemberKey,
    PlastLevel PlastLevel,
    DateOnly DateAchieved,
    bool IsPlastunka);
