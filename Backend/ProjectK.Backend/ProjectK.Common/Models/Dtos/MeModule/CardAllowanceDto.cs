using System.Text.Json.Serialization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Models.Dtos.MeModule;

/// <summary>
/// Whether a person may share their card, and how much of it. Someone 18 or older decides alone;
/// anyone younger, or with no date of birth, only as far as every kurin they stand in allows.
/// </summary>
public sealed record CardAllowanceDto(bool MayShare, bool IsAdult, [property: JsonConverter(typeof(JsonNumberEnumConverter<CardField>))] CardField MaxFields, bool MayLink);
