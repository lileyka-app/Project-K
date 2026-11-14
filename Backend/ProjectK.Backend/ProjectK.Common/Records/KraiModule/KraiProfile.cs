namespace ProjectK.Common.Records.KraiModule;

/// <summary>A край's profile with every value filled in: what the rest of Lilejka reads.</summary>
public sealed record KraiProfile(
    string KraiCode,
    string KraiName,
    int Revision,
    KraiCatalogSettings Catalog,
    KraiLocaleSettings Locale,
    KraiCardFieldsSettings CardFields,
    KraiDuesSettings Dues);
