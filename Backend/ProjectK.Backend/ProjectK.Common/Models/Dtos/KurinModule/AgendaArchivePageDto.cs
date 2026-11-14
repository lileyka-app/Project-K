using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>A page of the archive, newest first, and how long the kurin keeps it.</summary>
public sealed record AgendaArchivePageDto
{
    public required int Total { get; set; }
    public required List<AgendaItemDto> Items { get; set; }
    public required AgendaArchivePolicyDto Policy { get; set; }
}
