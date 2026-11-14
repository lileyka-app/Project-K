using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AgendaModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AgendaModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Features.Agenda;

/// <summary>How the board orders a column.</summary>
public enum AgendaBoardSort
{
    /// <summary>Nearest deadline first; tasks without one last.</summary>
    Due = 0,
    /// <summary>Last touched first.</summary>
    Recent = 1,
    /// <summary>Newest first.</summary>
    Created = 2,
    Title = 3
}

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

public static class AgendaBoardPagingRules
{
    public const int DefaultTake = 20;
    public const int MaxTake = 100;
}

/// <summary>
/// The live tasks the viewer may see, filtered, sorted and paged per column. Columns follow the
/// viewer's own state (<see cref="AgendaCompletionRules.ViewerStatus"/>), which only exists once the item is
/// read, so narrowing happens in memory over the live set — kept small by the archive.
/// </summary>
public sealed record GetAgendaBoardQuery(Guid KurinKey, AgendaBoardFilter? Filter = null)
    : IRequest<ServiceResult<AgendaBoardDto>>;

public sealed class GetAgendaBoardQueryHandler
    : IRequestHandler<GetAgendaBoardQuery, ServiceResult<AgendaBoardDto>>
{
    private static readonly AgendaItemStatus[] Columns = [AgendaItemStatus.Todo, AgendaItemStatus.InProgress, AgendaItemStatus.Done];

    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IOfficeDirectory _offices;
    private readonly IAgendaAccess _access;
    private readonly UserManager<AppUser> _userManager;

    public GetAgendaBoardQueryHandler(IUnitOfWork uow,
        IMemberDirectory members, IOfficeDirectory offices, IAgendaAccess access, UserManager<AppUser> userManager)
    {
        _uow = uow;
        _members = members;
        _offices = offices;
        _access = access;
        _userManager = userManager;
    }

    public async Task<ServiceResult<AgendaBoardDto>> Handle(GetAgendaBoardQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter ?? new AgendaBoardFilter();
        var take = PagingRules.Take(filter.Take, AgendaBoardPagingRules.MaxTake);
        var skip = PagingRules.Skip(filter.Skip);
        var viewer = await _access.BuildViewerAsync(request.KurinKey, cancellationToken);

        var items = (await _uow.AgendaItems.GetForViewerAsync(
            viewer.ToScope(),
            fromUtc: null,
            toUtc: null,
            onlyDated: false,
            kind: AgendaItemKind.Task,
            cancellationToken)).ToList();

        var lookups = await AgendaNames.LoadAsync(_uow, _members, _offices, request.KurinKey, items, cancellationToken);
        var roster = await AgendaRosterRules.LoadAsync(_uow, _offices, lookups.MemberGroups, items, cancellationToken);
        var userNames = await AgendaCreatorNames.ResolveAsync(_userManager, lookups.CreatorNames, items, cancellationToken);

        var all = items
            .Select(item => (Item: item, Response: AgendaItemMapping.Create(item, viewer, AgendaNames.KurinLabel, lookups.GroupNames, lookups.MemberNames, userNames, lookups.TargetLabels, lookups.Categories, roster)))
            .ToList();

        // The filter offers what is on the board, not every target in the kurin.
        var targets = all
            .SelectMany(row => row.Response.Assignments)
            .GroupBy(a => (a.TargetType, a.TargetKey))
            .Select(g => new AgendaBoardTargetDto { TargetType = g.Key.TargetType, TargetKey = g.Key.TargetKey, Label = g.First().Label ?? "—" })
            .OrderBy(t => t.TargetType switch { AgendaTargetType.Kurin => 0, AgendaTargetType.Leadership => 1, AgendaTargetType.Group => 2, _ => 3 })
            .ThenBy(t => t.Label)
            .ToList();

        var narrowed = all
            .Where(row => AgendaSearchRules.Matches(row.Response, filter.Search))
            .Where(row => filter.TargetKey is not { } key || row.Item.Assignments.Any(a => a.TargetKey == key && (filter.TargetType is null || a.TargetType == filter.TargetType)))
            .Where(row => !filter.OnlyMine || AgendaCompletionRules.StakeOf(row.Item, viewer) is not null)
            .Select(row => row.Response)
            .ToList();

        var columns = new List<AgendaBoardColumnDto>();
        foreach (var status in filter.Status is { } only ? [only] : Columns)
        {
            var inColumn = Sort(narrowed.Where(r => r.ViewerStatus == status), filter.Sort).ToList();
            columns.Add(new AgendaBoardColumnDto
            {
                Status = status,
                Total = inColumn.Count,
                Items = inColumn.Skip(filter.Status is null ? 0 : skip).Take(take).ToList()
            });
        }

        return new ServiceResult<AgendaBoardDto>(ResultType.Success, new AgendaBoardDto { Columns = columns, Targets = targets });
    }

    private static IEnumerable<AgendaItemDto> Sort(IEnumerable<AgendaItemDto> rows, AgendaBoardSort sort) => sort switch
    {
        AgendaBoardSort.Recent => rows.OrderByDescending(r => r.UpdatedUtc),
        AgendaBoardSort.Created => rows.OrderByDescending(r => r.CreatedUtc),
        AgendaBoardSort.Title => rows.OrderBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase),
        _ => rows.OrderBy(r => (r.EndUtc ?? r.StartUtc) is null).ThenBy(r => r.EndUtc ?? r.StartUtc).ThenBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase)
    };
}
