namespace ProjectK.Common.Models.Enums;

/// <summary>What a line the person adds to their life story is about.</summary>
public enum LifeEntryKind
{
    /// <summary>A вишкіл: a named course, its number and its комендант.</summary>
    Training = 1,

    /// <summary>A табір.</summary>
    Camp = 2,

    /// <summary>Any other захід: a мандрівка, a зустріч, an акція.</summary>
    Event = 3
}

/// <summary>
/// The вишколи the life story knows by name, shown by their acronyms only. Anything else is <see cref="Other"/>, named by the person.
/// The numbers are stored, so a new course takes a new number and none is ever reused.
/// </summary>
public enum TrainingCourse
{
    /// <summary>КВДЧ — кваліфікаційний вишкіл дійсного членства.</summary>
    Kvdch = 1,

    /// <summary>КВВ — крайовий вишкіл виховників.</summary>
    Kvv = 2,

    /// <summary>РОВ — Рада орлиного вогню, вишкіл новацьких виховників.</summary>
    Rov = 3,

    /// <summary>КВЗ — крайовий вишкіл звʼязкових.</summary>
    Kvz = 4,

    /// <summary>ЛШ — Лісова школа.</summary>
    LisovaShkola = 5,

    /// <summary>ШБ — Школа булавних.</summary>
    ShkolaBulavnykh = 6,

    Other = 99
}
