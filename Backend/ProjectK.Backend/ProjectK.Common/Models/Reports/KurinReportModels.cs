using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Reports;

/// <param name="Staff">
/// The кадра виховників, by the rule in <c>KurinRoster</c> — the same rule the реєстр splits on.
/// </param>
/// <param name="Youth">Everyone else in the kurin. Together with <paramref name="Staff"/> this is the whole склад.</param>
/// <param name="LevelTally">How many юнаки stand at each ступінь of this kurin's ladder.</param>
/// <param name="Members">
/// Everyone, staff included, in one list — this is what the per-person картотека pages are built
/// from, and кадра gets a картотека like anyone else.
/// </param>
/// <param name="ShowsTags">Whether the kurin uses tags, so the report has a place for them.</param>
public sealed record KurinReportData(
    KurinReportHeader Header,
    KurinReportKurin Kurin,
    IReadOnlyList<KurinReportGroup> Groups,
    IReadOnlyList<KurinReportMember> Staff,
    IReadOnlyList<KurinReportMember> Youth,
    IReadOnlyList<KurinReportLevelCount> LevelTally,
    IReadOnlyList<KurinReportMember> Members,
    bool ShowsTags);

/// <summary>One row of the чисельність table: a ступінь and how many юнаки stand at it.</summary>
public sealed record KurinReportLevelCount(string Label, int Count);

public sealed record KurinReportHeader(
    DateTime GeneratedAtUtc,
    string GeneratedByName,
    string? GeneratedByEmail,
    string BackendVersion,
    // Null for a release published without a code name; the footer then prints the version alone.
    string? BackendCodename);

public sealed record KurinReportKurin(
    Guid KurinKey,
    int? Number,
    string? Stanytsia,
    string? RegionOrCountry,
    string? NamedAfter,
    string? Description,
    string? SeniorBranches = null)
{
    /// <summary>A УСП or УПС kurin: named by its own name and its гілки, with no УПЮ number plate.</summary>
    public bool IsSenior => SeniorBranches is not null;
}

public sealed record KurinReportGroup(
    Guid GroupKey,
    string Name,
    string? Description,
    string? SilhouetteUrl,
    byte[]? SilhouetteBytes,
    IReadOnlyList<string> MentorNames,
    IReadOnlyList<KurinReportGroupMember> Members);

public sealed record KurinReportGroupMember(
    Guid MemberKey,
    string FullName,
    string Email,
    string? PhoneNumber,
    PlastLevel? LatestPlastLevel);

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
    IReadOnlyList<KurinReportProbePoint> SignedProbePoints,
    IReadOnlyList<KurinReportBadge> ConfirmedBadges,
    IReadOnlyList<KurinReportWarning> ActiveWarnings,
    IReadOnlyList<KurinReportAward> Awards,
    IReadOnlyList<KurinReportLeadershipHistory> LeadershipHistory,
    /// <summary>The kurin's tags on this person, by name. Empty when the kurin has tags off.</summary>
    IReadOnlyList<string> Tags);

public sealed record KurinReportPlastLevel(PlastLevel PlastLevel, DateOnly DateAchieved);

public sealed record KurinReportProbe(
    string ProbeId,
    string ProbeTitle,
    ProbeProgressStatus Status,
    string StatusLabel,
    DateTime? CompletedAtUtc,
    string? CompletedByName,
    DateTime? VerifiedAtUtc,
    string? VerifiedByName);

public sealed record KurinReportProbePoint(
    string ProbeId,
    string ProbeTitle,
    string PointId,
    string PointLabel,
    DateTime? SignedAtUtc,
    string? SignedByName,
    string? SignedByRole);

public sealed record KurinReportBadge(
    string BadgeId,
    string BadgeTitle,
    BadgeProgressStatus Status,
    string StatusLabel,
    DateTime? ReviewedAtUtc,
    string? ReviewedByName,
    string? ReviewedByRole);

public sealed record KurinReportWarning(
    MemberWarningLevel Level,
    string LevelLabel,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsRevoked);

public sealed record KurinReportAward(
    MemberAwardLevel? Level,
    string LevelLabel,
    DateTime DateAcquired,
    string? Note,
    BadgeProgressStatus Status,
    string StatusLabel);

public sealed record KurinReportLeadershipHistory(
    LeadershipType Type,
    string TypeLabel,
    LeadershipRole Role,
    string RoleLabel,
    string? ScopeName,
    DateOnly StartDate,
    DateOnly? EndDate);
