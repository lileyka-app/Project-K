namespace ProjectK.Infrastructure.Seeding.Demo;

/// <summary>What every pass shares: seeded picks, and dates counted from the seeder's now.</summary>
public partial class DemoDataSeeder
{
    private static readonly TimeZoneInfo Kyiv = ResolveKyiv();

    private T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];

    private string? PickDistinct(IReadOnlyList<string> items, HashSet<string> taken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = Pick(items);
            if (taken.Add(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private double Fraction(double min, double max) => min + _random.NextDouble() * (max - min);

    private DateTime DaysAgo(int days) => _now.AddDays(-days);

    private DateOnly YearsAgo(int years) => DateOnly.FromDateTime(_now.AddYears(-years));

    private static DateTime Later(DateTime a, DateTime b) => a > b ? a : b;

    private static DateTime Earlier(DateTime a, DateTime b) => a < b ? a : b;

    private int AgeOf(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(_now);
        var age = today.Year - dateOfBirth.Year;
        return dateOfBirth > today.AddYears(-age) ? age - 1 : age;
    }

    private static DateTime UtcDay(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private DateTime NextDate(int month, int day)
    {
        var candidate = UtcDay(_now.Year, month, day);
        return candidate.Date < _now.Date ? candidate.AddYears(1) : candidate;
    }

    private DateTime NextWeekday(DayOfWeek day)
    {
        var date = _now.Date.AddDays(1);
        while (date.DayOfWeek != day)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    private static DateTime PreviousOrSame(DateTime date, DayOfWeek day)
    {
        while (date.DayOfWeek != day)
        {
            date = date.AddDays(-1);
        }

        return date;
    }

    /// <summary>A wall-clock hour in Kyiv on the given day, as the UTC instant the calendar stores.</summary>
    private static DateTime ToUtc(DateTime day, int hour)
    {
        var local = DateTime.SpecifyKind(day.Date.AddHours(hour), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, Kyiv);
    }

    private static TimeZoneInfo ResolveKyiv()
    {
        foreach (var id in new[] { "Europe/Kyiv", "Europe/Kiev", "FLE Standard Time" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            {
                return zone;
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("Kyiv", TimeSpan.FromHours(2), "Kyiv", "Kyiv");
    }

    private string[] Shuffled(string[] source)
    {
        var copy = (string[])source.Clone();
        for (var i = copy.Length - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }

        return copy;
    }
}
