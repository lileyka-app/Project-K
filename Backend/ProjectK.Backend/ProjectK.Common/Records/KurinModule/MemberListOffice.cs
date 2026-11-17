using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// An office a person on a roster holds in the roster's kurin, as stored: the seat, its провід, and
/// both ends — the person's own term and the провід's. Which of these make someone кадра, and which
/// one names them, is decided by whoever reads the row.
/// </summary>
public sealed record MemberListOffice(
    Guid LeadershipHistoryKey,
    Guid MemberKey,
    Guid LeadershipKey,
    LeadershipRole Role,
    LeadershipType Type,
    Guid? OfficeKurinKey,
    string? GroupName,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly? OfficeEndDate);
