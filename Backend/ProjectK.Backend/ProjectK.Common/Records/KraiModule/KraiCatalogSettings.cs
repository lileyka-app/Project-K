namespace ProjectK.Common.Records.KraiModule;

public sealed record KraiCatalogSettings(
    IReadOnlyList<string> BadgeSources,
    IReadOnlyList<string> HiddenBadges);
