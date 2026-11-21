using ProjectK.Common.Enums.Shared;

namespace ProjectK.Common.Rules.Shared;

/// <summary>
/// What each ступінь is called, once, for everything the backend prints: the реєстр on screen,
/// the .xlsx it exports, and the звіт куреня in PDF.
/// <para>
/// It used to be called three different things — the report said «Прихильник», the registry
/// column said «Прихильник» too but «Заприсяження» for <see cref="PlastLevel.Uchasnyk"/>, and
/// the member card said «пл. прих.». Three tables meant three answers to one question, and a
/// провід reading the реєстр beside the звіт had to work out that they meant the same person.
/// These are the forms a провід actually writes.
/// </para>
/// <para>
/// <see cref="PlastLevel.Entry"/> is «пл. неім.» and not «Вступ до Пласту»: joining is when a
/// person becomes a пластун неіменований, so that date is the ступінь's date, not a separate
/// row standing before the ladder.
/// </para>
/// </summary>
public static class PlastLevelNames
{
    /// <summary>The ступінь's name, or its enum name if a new one was added and not named here.</summary>
    public static string Of(PlastLevel level) => Labels.GetValueOrDefault(level, level.ToString());

    /// <summary>
    /// The current ступінь's name: a старший пластун with a скоб from УПЮ is «ст. пл. скоб» or
    /// «ст. пл. гетьм. скоб» (<see cref="PlastLevelDegreeRules.CarriedSkob"/>).
    /// </summary>
    public static string Of(PlastLevel level, PlastLevel? carriedSkob) => (level, carriedSkob) switch
    {
        (PlastLevel.Starshoplastun, PlastLevel.HetmanskiySkob) => "ст. пл. гетьм. скоб",
        (PlastLevel.Starshoplastun, PlastLevel.Skob) => "ст. пл. скоб",
        _ => Of(level)
    };

    private static readonly IReadOnlyDictionary<PlastLevel, string> Labels =
        new Dictionary<PlastLevel, string>
        {
            [PlastLevel.Entry] = "пл. неім.",
            [PlastLevel.Prykhylnyk] = "пл. прих.",
            [PlastLevel.Uchasnyk] = "пл. уч.",
            [PlastLevel.Rozviduvach] = "пл. розв.",
            [PlastLevel.Skob] = "пл. скоб / вірл.",
            [PlastLevel.HetmanskiySkob] = "пл. гетьм. скоб / вірл.",
            [PlastLevel.Starshoplastun] = "ст. пл.",
            [PlastLevel.Senior] = "пл. сен.",
            [PlastLevel.SeniorPratsi] = "пл. сен. праці",
            [PlastLevel.SeniorDovirja] = "пл. сен. довір'я",
            [PlastLevel.SeniorKerivnytstva] = "пл. сен. керівництва"
        };
}
