namespace ProjectK.Common.Dtos.AgendaModule;

/// <summary>A kurin's archive rules; an empty period switches that step off.</summary>
public sealed record AgendaArchivePolicyDto
{
    public required int? AutoArchiveAfterDays { get; set; }
    public required int? PurgeAfterDays { get; set; }
}
