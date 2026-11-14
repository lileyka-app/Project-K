using ProjectK.Common.Enums.Shared;

namespace ProjectK.Common.Records.KurinModule;

public sealed record KurinReportGroupMember(
    Guid MemberKey,
    string FullName,
    string Email,
    string? PhoneNumber,
    PlastLevel? LatestPlastLevel);
