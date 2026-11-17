using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// One term a person held in a провід, with the провід it was in: its kind, where it sits, its own
/// name and when it closed. <see cref="StartDate"/> and <see cref="EndDate"/> are the term's own.
/// </summary>
public sealed record KurinReportSourceOffice(
    LeadershipType Type,
    Guid? KurinKey,
    Guid? GroupKey,
    string? Name,
    DateOnly? OfficeEndDate,
    LeadershipRole Role,
    DateOnly StartDate,
    DateOnly? EndDate);
