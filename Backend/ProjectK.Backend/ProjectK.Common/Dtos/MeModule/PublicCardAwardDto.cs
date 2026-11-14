namespace ProjectK.Common.Dtos.MeModule;

/// <summary>
/// One confirmed відзначення: an УПЮ one has <see cref="Level"/> 1 to 4 and its picture by level, a
/// senior one the catalogue's title and picture. <see cref="Ulad"/> is where it was earned —
/// <c>UPYu</c>, <c>USP</c> or <c>UPS</c> — or null for a senior one the catalogue gives to both.
/// </summary>
public sealed record PublicCardAwardDto(int? Level, string Title, string? ImageUrl, DateTime DateAcquired, string? Ulad);
