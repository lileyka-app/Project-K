using ProjectK.Common.Rules.KraiModule;

namespace ProjectK.Common.Dtos.KraiModule;

/// <summary>
/// One пільга of the край, such as «Брат чи сестра в курені» −20 %. A person given it pays the
/// full вкладка changed by this rule; the край's rules are copied onto each grant, so a later edit
/// here changes only what is granted after it.
/// </summary>
public sealed record KraiConcessionDocument
{
    /// <summary>Stays the same when the name changes: the grants point at it.</summary>
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary><see cref="KraiDues.Percent"/>, <see cref="KraiDues.Amount"/> or <see cref="KraiDues.Exempt"/>.</summary>
    public string Kind { get; init; } = KraiDues.Percent;

    /// <summary>The percent taken off, or the sum taken off per quarter. Unused for an exemption.</summary>
    public decimal? Value { get; init; }

    /// <summary>Which parts of the вкладка it lowers: <see cref="KraiDues.Parts"/>.</summary>
    public IReadOnlyList<string>? AppliesTo { get; init; }
}
