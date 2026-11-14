using ProjectK.Common.Rules.Shared;

namespace ProjectK.Common.Enums.Shared;

/// <summary>
/// A пластовий ступінь. The numbers are stored — the column is an <c>int</c> — and they are
/// frozen: a value inserted in the middle would renumber every row after it, and every учасник
/// in the database would quietly become a розвідувач. A new ступінь is appended with the next
/// free number and takes its real place in <see cref="PlastLadderRules"/>, which is what order means
/// here. Nothing may compare these values with &lt; or &gt;.
/// </summary>
public enum PlastLevel
{
    /// <summary>The day they joined Пласт at all.</summary>
    Entry = 0,
    Uchasnyk = 1,
    Rozviduvach = 2,
    Skob = 3,
    HetmanskiySkob = 4,
    Starshoplastun = 5,
    Senior = 6,
    SeniorPratsi = 7,
    SeniorDovirja = 8,
    SeniorKerivnytstva = 9,

    /// <summary>
    /// Candidate, between joining and заприсяження. Added after the others and therefore last by
    /// number, though it comes second on the ladder. Until it existed the report called
    /// <see cref="Entry"/> "Прихильник" while the member form called it "Дата вступу в Пласт" —
    /// one field standing for two different days.
    /// </summary>
    Prykhylnyk = 10
}
