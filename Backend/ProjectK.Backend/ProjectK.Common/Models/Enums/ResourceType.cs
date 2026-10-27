namespace ProjectK.Common.Models.Enums;

public enum ResourceType
{
    Group,
    Member,
    Kurin,
    PlanningSession,
    Leadership,
    MemberWarning,
    MemberAward,
    ProbeProgress,
    BadgeProgress,
    AgendaItem,

    /// <summary>A гурток's вкладка: its box, its operations, its rate. Keyed by the гурток.</summary>
    GroupDues,

    /// <summary>The kurin's own box and rates, and what гуртки hand up. Keyed by the kurin.</summary>
    KurinDues,

    /// <summary>A гурток's точкування: its sheets, its youths' points, what was given by hand. Keyed by the гурток.</summary>
    GroupScore,

    /// <summary>The kurin's точкування: the table of гуртки everyone sees, and its rules. Keyed by the kurin.</summary>
    KurinScore,

    /// <summary>What the КВ scores among themselves, out of the table and out of youths' sight. Keyed by the kurin.</summary>
    KurinScorePrivate,

    /// <summary>A member's email and phone. Keyed by the member; who reads it is the kurin's policy.</summary>
    MemberContacts,

    /// <summary>A member's full date of birth, year included. Keyed by the member.</summary>
    MemberBirthDate,

    /// <summary>The day and month a member was born. Keyed by the member.</summary>
    MemberBirthday,

    /// <summary>A member's address and school. Keyed by the member.</summary>
    MemberAddress,

    /// <summary>A member's parents and how to reach them. Keyed by the member.</summary>
    MemberParents,

    /// <summary>The extra card fields of a member's край. Keyed by the member.</summary>
    MemberExtras,

    /// <summary>The kurin's Журнал дій. Keyed by the kurin.</summary>
    KurinJournal,

    /// <summary>Which edition of the probe programme a гурток walks. Keyed by the гурток.</summary>
    GroupProbeEdition
}
