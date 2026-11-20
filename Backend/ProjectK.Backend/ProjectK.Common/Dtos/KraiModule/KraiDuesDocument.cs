using ProjectK.Common.Rules.KraiModule;

namespace ProjectK.Common.Dtos.KraiModule;

public sealed record KraiDuesDocument
{
    /// <summary>
    /// Where the kurins hand up the вкладка: <see cref="KraiDuesNames.Stanytsia"/>,
    /// <see cref="KraiDuesNames.Krai"/> or <see cref="KraiDuesNames.Both"/>.
    /// </summary>
    public string? Upstream { get; init; }

    /// <summary>The край's own пільги and знижки, which a гурток gives a person on top of the kurin's пільга.</summary>
    public IReadOnlyList<KraiConcessionDocument>? Concessions { get; init; }
}
