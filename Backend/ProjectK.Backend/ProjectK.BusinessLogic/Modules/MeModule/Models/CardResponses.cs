using System.Text.Json.Serialization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Models;

/// <summary>
/// Whether a person may share their card, and how much of it. Someone 18 or older decides alone;
/// anyone younger, or with no date of birth, only as far as every kurin they stand in allows.
/// </summary>
public sealed record CardAllowance(bool MayShare, bool IsAdult, [property: JsonConverter(typeof(JsonNumberEnumConverter<CardField>))] CardField MaxFields, bool MayLink);

public sealed record PublicCardKurin(int? Number, string? NamedAfter, string? GroupName, IReadOnlyList<KurinNumbering> Branches);

/// <summary>
/// One confirmed відзначення: an УПЮ one has <see cref="Level"/> 1 to 4 and its picture by level, a
/// senior one the catalogue's title and picture.
/// </summary>
public sealed record PublicCardAward(int? Level, string Title, string? ImageUrl, DateTime DateAcquired);

public sealed record PublicCardBadge(string Title, string? ImagePath, DateTime? ConfirmedAtUtc);

public sealed record PublicCardProbe(string Title, DateTime? VerifiedAtUtc);

/// <summary>
/// A card as anyone may see it: the name and only the parts the person chose and was allowed to
/// show. A part left out is null, not empty — empty means shown, but nothing there yet.
/// </summary>
public sealed record PublicCardDto(
    string FullName,
    string? PhotoUrl,
    IReadOnlyList<PublicCardKurin>? Kurins,
    string? Level,
    IReadOnlyList<PublicCardAward>? Awards,
    IReadOnlyList<PublicCardBadge>? Badges,
    IReadOnlyList<PublicCardProbe>? Probes,
    DateTime? CheckedAtUtc);

/// <summary>The person's own view of their card: what they may do, what they chose, and how it looks.</summary>
public sealed record MyCardDto(
    CardAllowance Allowance,
    [property: JsonConverter(typeof(JsonNumberEnumConverter<CardField>))] CardField Fields,
    string? LinkUrl,
    DateTime? LinkIssuedAtUtc,
    PublicCardDto Preview);

/// <summary>A QR and the address behind it; <see cref="ExpiresAtUtc"/> is set when it stops working.</summary>
public sealed record CardCodeDto(string Url, string QrCode, DateTime? ExpiresAtUtc);
