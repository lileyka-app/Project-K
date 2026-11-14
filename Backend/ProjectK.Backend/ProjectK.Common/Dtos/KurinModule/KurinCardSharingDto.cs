using System.Text.Json.Serialization;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>What the kurin lets its people under 18 share. A kurin that never set it lets them share nothing.</summary>
public sealed record KurinCardSharingDto(
    bool MinorsMayShare,
    [property: JsonConverter(typeof(JsonNumberEnumConverter<CardField>))] CardField MinorFields,
    bool MinorsMayLink);
