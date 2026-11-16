using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Models;

/// <summary>
/// What the board narrows to. <see cref="Status"/> asks for one column only — the «Завантажити ще»
/// of that column; without it every column comes back with its first page.
/// </summary>
public sealed record AgendaBoardFilter
{
    public string? Search { get; init; }
    public AgendaTargetType? TargetType { get; init; }
    public Guid? TargetKey { get; init; }
    /// <summary>Only tasks where something is the viewer's own to do.</summary>
    public bool OnlyMine { get; init; }
    public AgendaBoardSort Sort { get; init; } = AgendaBoardSort.Due;
    public AgendaItemStatus? Status { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; } = AgendaBoardPagingRules.DefaultTake;
}
