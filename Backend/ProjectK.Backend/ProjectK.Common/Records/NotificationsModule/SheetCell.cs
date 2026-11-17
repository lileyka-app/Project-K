namespace ProjectK.Common.Records.NotificationsModule;

/// <summary>
/// One cell. A date is kept as a date and a count as a number, so the spreadsheet sorts, formats and
/// sums them as what they are instead of as text that merely looks like them.
/// </summary>
public readonly record struct SheetCell(string? Text, DateOnly? Date, int? Number)
{
    public static SheetCell Of(string? text) => new(text, null, null);

    public static SheetCell Of(DateOnly? date) => new(null, date, null);

    public static SheetCell Count(int number) => new(null, null, number);
}
