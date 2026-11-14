namespace ProjectK.Common.Records.ArchiveModule;

/// <summary>A stretch in one гурток. A null start is one already running when the record began.</summary>
public sealed record ArchivedStint(Guid GroupKey, string? GroupName, DateTime? FromUtc, DateTime? ToUtc);
