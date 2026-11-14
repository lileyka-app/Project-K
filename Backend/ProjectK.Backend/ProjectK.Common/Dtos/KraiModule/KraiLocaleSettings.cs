namespace ProjectK.Common.Dtos.KraiModule;

public sealed record KraiLocaleSettings(
    string Currency,
    string TimeZone,
    string NumberLocale);
