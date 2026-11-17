namespace ProjectK.Common.Dtos.KraiModule;

public sealed record KraiLocaleDocument
{
    /// <summary>ISO 4217 code, such as UAH or CZK.</summary>
    public string? Currency { get; init; }

    /// <summary>IANA zone, such as Europe/Prague.</summary>
    public string? TimeZone { get; init; }

    /// <summary>How numbers and dates are written, such as uk-UA.</summary>
    public string? NumberLocale { get; init; }
}
