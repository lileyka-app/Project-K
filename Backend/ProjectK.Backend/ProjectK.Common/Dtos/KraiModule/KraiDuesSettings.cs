using ProjectK.Common.Records.DuesModule;
using ProjectK.Common.Rules.KraiModule;

namespace ProjectK.Common.Dtos.KraiModule;

/// <summary>Where the kurins of the край hand up their вкладка.</summary>
public sealed record KraiDuesSettings(string Upstream)
{
    public static readonly KraiDuesSettings Default = new(KraiDues.Stanytsia);

    public bool PaysStanytsia => Upstream != KraiDues.Krai;
    public bool PaysKrai => Upstream != KraiDues.Stanytsia;

    /// <summary>The край's пільги a гурток may give, with valid rules only.</summary>
    public IReadOnlyList<DuesConcessionRule> Concessions { get; init; } = [];
}
