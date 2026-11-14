using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.KurinModule;

public sealed record AgendaBoardColumnDto
{
    public required AgendaItemStatus Status { get; set; }

    /// <summary>How many tasks the column holds under the filter — the «Завантажити ще» counts against it.</summary>
    public required int Total { get; set; }

    public required List<AgendaItemDto> Items { get; set; }
}
