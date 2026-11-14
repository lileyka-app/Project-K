using ProjectK.BusinessLogic.Modules.RosterModule.Features.Import;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Modules.RosterModule.Services;

/// <summary>
/// A guess at what a column is, from the words in its heading. Only a guess — the провід confirms
/// or changes every one of them on the mapping screen, which is what lets the import read a table
/// nobody wrote for us.
/// </summary>
internal static class RosterHeaderRules
{
    /// <summary>
    /// Ordered: the first needle found wins, so «дата народження» is matched before the bare
    /// «дата», and «дата вступу» before «вступ».
    /// </summary>
    private static readonly (string Needle, RosterField Field, PlastLevel? Level)[] Needles =
    [
        ("по батьк", RosterField.MiddleName, null),
        ("прізвищ", RosterField.LastName, null),
        ("призвищ", RosterField.LastName, null),
        ("народж", RosterField.DateOfBirth, null),
        ("др", RosterField.DateOfBirth, null),

        ("гетьман", RosterField.LevelDate, PlastLevel.HetmanskiySkob),
        ("старшопласт", RosterField.LevelDate, PlastLevel.Starshoplastun),
        ("перехід в усп", RosterField.LevelDate, PlastLevel.Starshoplastun),
        ("перехід в упс", RosterField.LevelDate, PlastLevel.Senior),
        ("сен. кер", RosterField.LevelDate, PlastLevel.SeniorKerivnytstva),
        ("сен. дов", RosterField.LevelDate, PlastLevel.SeniorDovirja),
        ("сен. пр", RosterField.LevelDate, PlastLevel.SeniorPratsi),
        ("заприсяж", RosterField.LevelDate, PlastLevel.Uchasnyk),
        ("прихильник", RosterField.LevelDate, PlastLevel.Prykhylnyk),
        ("розвідувач", RosterField.LevelDate, PlastLevel.Rozviduvach),
        ("скоб", RosterField.LevelDate, PlastLevel.Skob),
        ("вступ", RosterField.LevelDate, PlastLevel.Entry),

        ("дата ступ", RosterField.PlastLevelDate, null),
        ("ступ", RosterField.PlastLevel, null),
        ("курін", RosterField.KurinNumber, null),
        ("курен", RosterField.KurinNumber, null),
        ("гурт", RosterField.GroupName, null),
        ("телефон", RosterField.PhoneNumber, null),
        ("моб", RosterField.PhoneNumber, null),
        ("пошт", RosterField.Email, null),
        ("email", RosterField.Email, null),
        ("mail", RosterField.Email, null),
        ("адрес", RosterField.Address, null),
        ("школ", RosterField.School, null),
        ("навчанн", RosterField.School, null),
        ("ім", RosterField.FirstName, null),
        ("им", RosterField.FirstName, null),
        ("пiб", RosterField.LastName, null),
        ("піб", RosterField.LastName, null)
    ];

    public static (RosterField Field, PlastLevel? Level) Guess(string? header)
    {
        var text = header?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(text))
        {
            return (RosterField.Ignore, null);
        }

        foreach (var (needle, field, level) in Needles)
        {
            if (text.Contains(needle, StringComparison.Ordinal))
            {
                return (field, level);
            }
        }

        return (RosterField.Ignore, null);
    }
}
