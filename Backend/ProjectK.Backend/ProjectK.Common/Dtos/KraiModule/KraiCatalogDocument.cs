namespace ProjectK.Common.Dtos.KraiModule;

public sealed record KraiCatalogDocument
{
    /// <summary>Which краї's вмілості the kurins see, by the catalogue's own krai mark.</summary>
    /// <remarks>Проби are not set here: the three юнацькі проби are the same across Plast.</remarks>
    public IReadOnlyList<string>? BadgeSources { get; init; }

    /// <summary>Single вмілості the край leaves out of what its sources give.</summary>
    public IReadOnlyList<string>? HiddenBadges { get; init; }
}
