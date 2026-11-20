using ProjectK.Common.Enums.Shared;

namespace ProjectK.Common.Records.KurinModule;

public sealed record KurinReportMember(
    Guid MemberKey,
    Guid? UserKey,
    Guid? GroupKey,
    string? GroupName,
    string FullName,
    string Initials,
    string Email,
    string PhoneNumber,
    DateOnly DateOfBirth,
    string? Address,
    string? School,
    string? ProfilePhotoUrl,
    byte[]? ProfilePhotoBytes,
    PlastLevel? LatestPlastLevel,
    /// <summary>The гуртки this person runs as виховник. Empty for a юнак.</summary>
    IReadOnlyList<string> MentoredGroupNames,
    IReadOnlyList<KurinReportPlastLevel> PlastLevels,
    IReadOnlyList<KurinReportProbe> Probes,
    IReadOnlyList<KurinReportBadge> ConfirmedBadges,
    IReadOnlyList<KurinReportWarning> ActiveWarnings,
    IReadOnlyList<KurinReportAward> Awards,
    IReadOnlyList<KurinReportLeadershipHistory> LeadershipHistory,
    /// <summary>The kurin's tags on this person, by name. Empty when the kurin has tags off.</summary>
    IReadOnlyList<string> Tags);
