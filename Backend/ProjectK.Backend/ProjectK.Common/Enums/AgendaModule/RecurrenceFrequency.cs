namespace ProjectK.Common.Enums.AgendaModule;

/// <summary>How an agenda item repeats. <c>None</c> is a one-off; the rest step by a whole interval.</summary>
public enum RecurrenceFrequency
{
    None = 0,
    Weekly = 1,
    Monthly = 2,
    Yearly = 3
}
