namespace ProjectK.Common.Dtos.ArchiveModule;

/// <param name="GroupKey"></param>
/// <param name="GroupName">Null once the гурток is deleted for good.</param>
/// <param name="From">Null for a stretch already running when the record began.</param>
/// <param name="To"></param>
public sealed record ArchivedStintDto(Guid GroupKey, string? GroupName, DateOnly? From, DateOnly? To);
