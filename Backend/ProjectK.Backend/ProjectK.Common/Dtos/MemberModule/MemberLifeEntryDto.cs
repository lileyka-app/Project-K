using ProjectK.Common.Enums.MemberModule;


namespace ProjectK.Common.Dtos.MemberModule;

/// <summary>A вишкіл, табір or захід from the person's own life story.</summary>
public sealed record MemberLifeEntryDto(
    Guid MemberLifeEntryKey,
    LifeEntryKind Kind,
    TrainingCourse? Course,
    string? Title,
    int? Number,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Place,
    string? Commandant,
    string? Role);
