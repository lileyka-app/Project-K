using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>A kurin's archive rules; an empty period switches that step off.</summary>
public sealed record AgendaArchivePolicyDto
{
    public required int? AutoArchiveAfterDays { get; set; }
    public required int? PurgeAfterDays { get; set; }
}
