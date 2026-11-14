using System.Text.Json.Serialization;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.MeModule;

/// <summary>The person's own view of their card: what they may do, what they chose, and how it looks.</summary>
public sealed record MyCardDto(
    CardAllowanceDto Allowance,
    [property: JsonConverter(typeof(JsonNumberEnumConverter<CardField>))] CardField Fields,
    string? LinkUrl,
    DateTime? LinkIssuedAtUtc,
    PublicCardDto Preview);
