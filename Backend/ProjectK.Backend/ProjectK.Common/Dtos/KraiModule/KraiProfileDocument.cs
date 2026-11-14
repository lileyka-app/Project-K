using ProjectK.Common.Records.KraiModule;

namespace ProjectK.Common.Dtos.KraiModule;

/// <summary>
/// A край's profile as it is stored: one JSON document per revision. Every value is optional; a
/// value left out is taken from code, so an older document still reads after Lilejka grows a new
/// setting. Adding a setting means a nullable field here, its default in
/// <c>KraiProfileProvider</c> and its place in <see cref="KraiProfile"/>.
/// </summary>
public sealed record KraiProfileDocument
{
    public const int CurrentSchemaVersion = 1;

    public KraiCatalogDocument? Catalog { get; init; }
    public KraiLocaleDocument? Locale { get; init; }
    public KraiCardFieldsDocument? CardFields { get; init; }
    public KraiDuesDocument? Dues { get; init; }
}
