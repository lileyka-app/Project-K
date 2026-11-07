namespace ProjectK.Common.Models.Enums;

/// <summary>
/// Which catalogue ступінь a <see cref="PlastLevel"/> is, for a пластун or a пластунка: the catalogue
/// draws them apart («Старший пластун», «Старша пластунка»), the ladder does not.
/// </summary>
public static class PlastLevelDegrees
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

    /// <summary>The catalogue id, or null for a level with no відзнака (<see cref="PlastLevel.Entry"/>).</summary>
    public static string? CatalogId(PlastLevel level, bool isPlastunka) =>
        Ids.TryGetValue(level, out var ids) ? (isPlastunka ? ids.Plastunka : ids.Plastun) : null;
}
