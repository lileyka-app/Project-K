using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Records;

/// <summary>
/// One confirmed відзначення and when it was earned: an УПЮ one by <see cref="Level"/>, a senior one
/// by <see cref="CatalogId"/>.
/// </summary>
public sealed record ConfirmedAward(MemberAwardLevel? Level, string? CatalogId, DateTime DateAcquired);
