namespace ProjectK.Common.Dtos.KraiModule;

public sealed record KraiCatalogSettings(
    IReadOnlyList<string> BadgeSources,
    IReadOnlyList<string> HiddenBadges);
