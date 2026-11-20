namespace ProjectK.Common.Enums.MemberModule;

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
