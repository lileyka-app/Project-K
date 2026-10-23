using System.Globalization;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Import;

/// <summary>
/// Turning what people write in a roster into what the system stores. Every method here answers
/// "or null" rather than throwing: a value it cannot read is a rejected row with a reason, not a
/// failed import.
/// </summary>
internal static class RosterValues
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd",   // what the reader emits for a real Excel date
        "dd.MM.yyyy",
        "d.M.yyyy",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "dd-MM-yyyy",
        "yyyy.MM.dd",
        "dd.MM.yy",
        "d.M.yy"
    ];

    /// <summary>
    /// Ступінь by the words rosters actually use. Matching is loose on purpose — «пл. розв.»,
    /// «Розвідувач» and «розвідувачка» are the same answer, and a роster written by hand will
    /// have all three.
    /// </summary>
    private static readonly (string Needle, PlastLevel Level)[] LevelNeedles =
    [
        ("сен. кер", PlastLevel.SeniorKerivnytstva),
        ("сеніор кер", PlastLevel.SeniorKerivnytstva),
        ("керівництв", PlastLevel.SeniorKerivnytstva),
        ("сен. дов", PlastLevel.SeniorDovirja),
        ("сеніор дов", PlastLevel.SeniorDovirja),
        ("довір", PlastLevel.SeniorDovirja),
        ("сен. пр", PlastLevel.SeniorPratsi),
        ("сеніор пр", PlastLevel.SeniorPratsi),
        ("прац", PlastLevel.SeniorPratsi),
        // "гетьм." is how the app itself prints it, and a roster pasted out of our own screens is
        // the likeliest file of all — so the short forms have to parse before the long ones.
        ("гетьм", PlastLevel.HetmanskiySkob),
        ("старш", PlastLevel.Starshoplastun),
        ("ст. пл", PlastLevel.Starshoplastun),
        ("усп", PlastLevel.Starshoplastun),
        ("сеніор", PlastLevel.Senior),
        ("сен.", PlastLevel.Senior),
        ("упс", PlastLevel.Senior),
        ("скоб", PlastLevel.Skob),
        ("вірлиц", PlastLevel.Skob),
        ("розв", PlastLevel.Rozviduvach),
        ("учасн", PlastLevel.Uchasnyk),
        ("уч.", PlastLevel.Uchasnyk),
        ("прихил", PlastLevel.Prykhylnyk),
        ("прих", PlastLevel.Prykhylnyk),
        ("вступ", PlastLevel.Entry)
    ];

    public static string? Text(string? raw)
    {
        var trimmed = raw?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static DateOnly? Date(string? raw)
    {
        var text = Text(raw);
        if (text is null)
        {
            return null;
        }

        foreach (var format in DateFormats)
        {
            if (DateOnly.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed;
            }
        }

        // Excel hands a bare number when the cell was never formatted as a date. 1899-12-30 is
        // its day zero, and the 1900 leap-year bug is already baked into that offset.
        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial)
            && serial > 0
            && serial < 2958466)
        {
            return DateOnly.FromDateTime(new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Utc).AddDays(serial));
        }

        return null;
    }

    /// <summary>The ступінь a roster's word names, or null when nothing here recognises it.</summary>
    public static PlastLevel? Level(string? raw)
    {
        var text = Text(raw)?.ToLowerInvariant();
        if (text is null)
        {
            return null;
        }

        foreach (var (needle, level) in LevelNeedles)
        {
            if (text.Contains(needle, StringComparison.Ordinal))
            {
                return level;
            }
        }

        return null;
    }

    /// <summary>The kurin's number out of «14», «14 курінь» or «к. ч. 14».</summary>
    public static int? KurinNumber(string? raw)
    {
        var text = Text(raw);
        if (text is null)
        {
            return null;
        }

        var digits = new string(text.TakeWhile(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            digits = new string(text.SkipWhile(character => !char.IsDigit(character))
                .TakeWhile(char.IsDigit)
                .ToArray());
        }

        return int.TryParse(digits, out var number) ? number : null;
    }
}
