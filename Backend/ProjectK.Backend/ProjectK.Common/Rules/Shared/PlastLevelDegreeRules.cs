using ProjectK.Common.Enums.Shared;

namespace ProjectK.Common.Rules.Shared;

/// <summary>
/// Which catalogue ступінь a <see cref="PlastLevel"/> is, for a пластун or a пластунка: the catalogue
/// draws them apart («Старший пластун», «Старша пластунка»), the ladder does not.
/// </summary>
public static class PlastLevelDegreeRules
{
    private static readonly IReadOnlyDictionary<PlastLevel, (string Plastun, string Plastunka)> Ids =
        new Dictionary<PlastLevel, (string, string)>
        {
            [PlastLevel.Prykhylnyk] = ("upyu-prykhylnyk", "upyu-prykhylnytsia"),
            [PlastLevel.Uchasnyk] = ("upyu-uchasnyk", "upyu-uchasnytsia"),
            [PlastLevel.Rozviduvach] = ("upyu-rozviduvach", "upyu-rozviduvachka"),
            [PlastLevel.Skob] = ("upyu-skob", "upyu-virlytsia"),
            [PlastLevel.HetmanskiySkob] = ("upyu-hetmanskyi-skob", "upyu-hetmanska-virlytsia"),
            [PlastLevel.Starshoplastun] = ("usp-starshyi-plastun", "usp-starsha-plastunka"),
            [PlastLevel.Senior] = ("ups-senior-prykhylnyk", "ups-seniorka-prykhylnytsia"),
            [PlastLevel.SeniorPratsi] = ("ups-senior-pratsi", "ups-seniorka-pratsi"),
            [PlastLevel.SeniorDovirja] = ("ups-senior-doviria", "ups-seniorka-doviria"),
            [PlastLevel.SeniorKerivnytstva] = ("ups-senior-kerivnytstva", "ups-seniorka-kerivnytstva"),
        };

    /// <summary>
    /// The скоб a старший пластун carries from УПЮ — the гетьманський before the plain one; null without
    /// either. It goes with the ступінь: «ст. пл. скоб», drawn as the скоб.
    /// </summary>
    public static PlastLevel? CarriedSkob(IEnumerable<PlastLevel> held)
    {
        var levels = held.ToHashSet();
        return levels.Contains(PlastLevel.HetmanskiySkob) ? PlastLevel.HetmanskiySkob
            : levels.Contains(PlastLevel.Skob) ? PlastLevel.Skob
            : null;
    }

    /// <summary>How the carried скоб reads after «Старший пластун» or «Старша пластунка».</summary>
    public static string CarriedSkobName(PlastLevel skob, bool isPlastunka) => (skob, isPlastunka) switch
    {
        (PlastLevel.HetmanskiySkob, false) => "гетьманський скоб",
        (PlastLevel.HetmanskiySkob, true) => "гетьманська вірлиця",
        (_, false) => "скоб",
        (_, true) => "вірлиця"
    };

    /// <summary>The catalogue id, or null for a level with no відзнака (<see cref="PlastLevel.Entry"/>).</summary>
    public static string? CatalogId(PlastLevel level, bool isPlastunka) =>
        Ids.TryGetValue(level, out var ids) ? (isPlastunka ? ids.Plastunka : ids.Plastun) : null;
}
