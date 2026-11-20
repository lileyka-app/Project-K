using ProjectK.Common.Enums.Shared;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// A person as the kurin's report reads them: their record, ступені, перестороги, відзначення and
/// offices, every kurin's alike. Values only, so the report never holds the member module's entities.
/// </summary>
public sealed record KurinReportSourceMember(
    Guid MemberKey,
    Guid? UserKey,
    string FirstName,
    string? MiddleName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateOnly DateOfBirth,
    string? Address,
    string? School,
    string? ProfilePhotoBlobName,
    PlastLevel? LatestPlastLevel,
    IReadOnlyList<KurinReportPlastLevel> Levels,
    IReadOnlyList<KurinReportSourceWarning> Warnings,
    IReadOnlyList<KurinReportSourceAward> Awards,
    IReadOnlyList<KurinReportSourceOffice> Offices);
