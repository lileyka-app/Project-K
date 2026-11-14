using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.KurinModule;

/// <summary>
/// A вишкіл, табір or захід the person wrote into their own life story. Only they read and change it:
/// the life story is theirs, and nobody in any kurin keeps it for them.
/// </summary>
public class MemberLifeEntry : Entity
{
    public const int TitleMaxLength = 160;
    public const int PersonMaxLength = 120;
    public const int RoleMaxLength = 80;
    public const int PlaceMaxLength = 120;

    public Guid MemberLifeEntryKey { get; set; } = Guid.NewGuid();
    public Guid MemberKey { get; set; }
    public Member Member { get; set; } = null!;

    public LifeEntryKind Kind { get; set; }

    /// <summary>Which вишкіл; set for a <see cref="LifeEntryKind.Training"/> only.</summary>
    public TrainingCourse? Course { get; set; }

    /// <summary>The name: of a табір or захід, or of a вишкіл that is <see cref="TrainingCourse.Other"/>.</summary>
    public string? Title { get; set; }

    /// <summary>The course's own running number — the 12th КВЗ is 12.</summary>
    public int? Number { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public string? Place { get; set; }

    /// <summary>Who was комендант.</summary>
    public string? Commandant { get; set; }

    /// <summary>What the person was there: учасник, булавний, інструктор.</summary>
    public string? Role { get; set; }
}
