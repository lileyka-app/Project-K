namespace ProjectK.Common.Models.Enums;

/// <summary>The parts of a quarter's вкладка a пільга of the край lowers. Stored as a number.</summary>
[Flags]
public enum DuesParts
{
    None = 0,
    Stanytsia = 1,
    Krai = 2,
    Kurin = 4,
    Group = 8
}
