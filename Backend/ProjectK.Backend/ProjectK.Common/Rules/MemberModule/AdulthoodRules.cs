namespace ProjectK.Common.Rules.MemberModule;

/// <summary>
/// When a person is an adult: what gives them rights over themselves — sharing their own card,
/// recording their own senior відзначення. The one place the age of 18 is counted.
/// </summary>
public static class AdulthoodRules
{
    /// <summary>The age a person comes of age at.</summary>
    public const int AdultAge = 18;

    /// <summary>
    /// Whether someone born on <paramref name="born"/> is an adult on <paramref name="today"/>. No date,
    /// or the empty one a record without a birthday carries, means a minor.
    /// </summary>
    public static bool IsAdult(DateOnly? born, DateOnly today) =>
        born is { } date && date != default && date.AddYears(AdultAge) <= today;
}
