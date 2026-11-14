using ProjectK.Common.Records.DuesModule;

namespace ProjectK.Common.Rules.DuesModule;

/// <summary>
/// Which пластовий рік a quarter is shown under. Dues are counted in calendar quarters; the year only
/// groups them in the table, so no money depends on it.
/// <para>
/// The year starts on 1 September, inside the third quarter. Which side that quarter lands on is a
/// decision, not derived, so it lives only in <see cref="OpeningQuarter"/>; every grouping follows it.
/// </para>
/// </summary>
public static class PlastYearRules
{
    /// <summary>
    /// The quarter that opens a пластовий рік. IV: the third quarter (July–September) still belongs
    /// to the year that is ending, so 25–26 is IV 2025 and I–III 2026.
    /// </summary>
    public const int OpeningQuarter = 4;

    /// <summary>The calendar year the пластовий рік starts in: 2025 for 25–26.</summary>
    public static int Of(DuesQuarter quarter) =>
        quarter.Number >= OpeningQuarter ? quarter.Year : quarter.Year - 1;

    /// <summary>The calendar year the пластовий рік of that day starts in.</summary>
    public static int Of(DateOnly date) => Of(DuesQuarter.Of(date));

    /// <summary>The first day of the пластовий рік that starts in <paramref name="startYear"/>.</summary>
    public static DateOnly FirstDay(int startYear) => new DuesQuarter(startYear, OpeningQuarter).FirstDay;

    /// <summary>Its last day, inclusive.</summary>
    public static DateOnly LastDay(int startYear) => FirstDay(startYear + 1).AddDays(-1);

    /// <summary>The four quarters of the пластовий рік that starts in <paramref name="startYear"/>.</summary>
    public static IReadOnlyList<DuesQuarter> QuartersOf(int startYear)
    {
        var first = new DuesQuarter(startYear, OpeningQuarter);
        return [first, DuesQuarter.FromIndex(first.Index + 1), DuesQuarter.FromIndex(first.Index + 2), DuesQuarter.FromIndex(first.Index + 3)];
    }

    /// <summary>"25–26".</summary>
    public static string Label(int startYear) => $"{startYear % 100:00}–{(startYear + 1) % 100:00}";
}
