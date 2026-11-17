namespace ProjectK.Common.Options.KurinModule;

/// <summary>
/// Which numbers a kurin may carry. The floor is fixed at one, so zero and below stay free for
/// kurins the team keeps for itself (a test kurin on prod); the ceiling is configuration, so it can
/// grow with the станиця instead of letting anyone open kurin 1153.
/// </summary>
public sealed class KurinNumberOptions
{
    public const string SectionName = "KurinNumbers";

    public const int MinNumber = 1;

    public int MaxNumber { get; init; } = 200;

    public bool IsAllowed(int number) => number >= MinNumber && number <= MaxNumber;

    /// <summary>The one answer to a number outside <see cref="IsAllowed"/>, wherever a number is checked.</summary>
    public string OutOfRangeMessage => $"Kurin number must be between {MinNumber} and {MaxNumber}.";
}
