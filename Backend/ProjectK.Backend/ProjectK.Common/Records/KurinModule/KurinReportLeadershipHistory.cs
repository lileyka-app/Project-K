using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Records.KurinModule;

public sealed record KurinReportLeadershipHistory(
    LeadershipType Type,
    string TypeLabel,
    LeadershipRole Role,
    string RoleLabel,
    string? ScopeName,
    DateOnly StartDate,
    DateOnly? EndDate);
