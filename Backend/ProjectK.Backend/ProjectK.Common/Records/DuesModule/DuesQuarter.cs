namespace ProjectK.Common.Records.DuesModule;

/// <summary>
/// A calendar quarter — the unit dues are charged in. Stored as one number, <see cref="Index"/>, so a
/// range of quarters is a range of integers and needs no date arithmetic in SQL.
/// </summary>
public readonly record struct DuesQuarter : IComparable<DuesQuarter>
{
    public DuesQuarter(int year, int number)
    {
        if (number is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "A quarter is numbered 1 to 4.");
        }

        Year = year;
        Number = number;
    }

    public int Year { get; }

    /// <summary>1 for January–March … 4 for October–December.</summary>
    public int Number { get; }

    /// <summary>What the database keeps: <c>Year * 4 + Number - 1</c>.</summary>
    public int Index => (Year * 4) + Number - 1;

    public static DuesQuarter FromIndex(int index) => new(index / 4, (index % 4) + 1);

    public static DuesQuarter Of(DateOnly date) => new(date.Year, ((date.Month - 1) / 3) + 1);

    public static DuesQuarter Of(DateTime date) => Of(DateOnly.FromDateTime(date));

    public DuesQuarter Next() => FromIndex(Index + 1);

    public DateOnly FirstDay => new(Year, ((Number - 1) * 3) + 1, 1);

    public int CompareTo(DuesQuarter other) => Index.CompareTo(other.Index);

    public static bool operator <(DuesQuarter left, DuesQuarter right) => left.Index < right.Index;
    public static bool operator >(DuesQuarter left, DuesQuarter right) => left.Index > right.Index;
    public static bool operator <=(DuesQuarter left, DuesQuarter right) => left.Index <= right.Index;
    public static bool operator >=(DuesQuarter left, DuesQuarter right) => left.Index >= right.Index;

    public override string ToString() => $"{Year}-Q{Number}";
}
