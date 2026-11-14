using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AgendaModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Features.Agenda;

/// <summary>
/// Calendar feed: dated items in the window that the current user may see. With
/// <see cref="IncludeKurinSchedules"/> («Графіки гуртків») it also carries every event of a group
/// marked «графік куреня», as <see cref="AgendaAudience.Schedule"/>; without it those are left out
/// for everyone, whole-kurin viewers too, so the switch means the same to all.
/// </summary>
public sealed record GetAgendaItemsQuery(Guid KurinKey, DateTime? FromUtc, DateTime? ToUtc, bool IncludeKurinSchedules = false)
    : IRequest<ServiceResult<IEnumerable<AgendaItemDto>>>;

public sealed class GetAgendaItemsQueryHandler
    : IRequestHandler<GetAgendaItemsQuery, ServiceResult<IEnumerable<AgendaItemDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IOfficeDirectory _offices;
    private readonly IAgendaAccess _access;
    private readonly UserManager<AppUser> _userManager;
    private readonly TimeProvider _timeProvider;
    private readonly IKraiProfileDirectory _profiles;

    public GetAgendaItemsQueryHandler(IUnitOfWork uow,
        IMemberDirectory members, IOfficeDirectory offices, IAgendaAccess access, UserManager<AppUser> userManager, TimeProvider timeProvider,
        IKraiProfileDirectory profiles)
    {
        _uow = uow;
        _members = members;
        _offices = offices;
        _access = access;
        _userManager = userManager;
        _timeProvider = timeProvider;
        _profiles = profiles;
    }

    public async Task<ServiceResult<IEnumerable<AgendaItemDto>>> Handle(GetAgendaItemsQuery request, CancellationToken cancellationToken)
    {
        var viewer = await _access.BuildViewerAsync(request.KurinKey, cancellationToken);

        var items = (await _uow.AgendaItems.GetForViewerAsync(
            viewer.ToScope(),
            request.FromUtc,
            request.ToUtc,
            onlyDated: true,
            kind: null,
            cancellationToken,
            includeKurinSchedules: request.IncludeKurinSchedules)).ToList();

        var lookups = await AgendaNames.LoadAsync(_uow, _members, _offices, request.KurinKey, items, cancellationToken);
        var creatorNames = await AgendaCreatorNames.ResolveAsync(_userManager, lookups.CreatorNames, items, cancellationToken);
        var roster = await AgendaRosterRules.LoadAsync(_uow, _offices, lookups.MemberGroups, items, cancellationToken);

        // Recurring items are expanded into one row per occurrence inside the query window; one-offs pass
        // through unchanged. A missing window is bounded so an open-ended series can't expand forever.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var windowFrom = request.FromUtc ?? now.AddMonths(-6);
        var windowTo = request.ToUtc ?? now.AddMonths(12);
        var zone = await _profiles.ForKurinAsync(request.KurinKey, cancellationToken);

        var responses = items
            .SelectMany(item => item.RecurrenceFrequency == RecurrenceFrequency.None
                ? new[] { AgendaItemMapping.Create(item, viewer, AgendaNames.KurinLabel, lookups.GroupNames, lookups.MemberNames, creatorNames, lookups.TargetLabels, lookups.Categories, roster) }
                : AgendaRecurrenceRules.Expand(item, windowFrom, windowTo, zone)
                    .Select(occ => AgendaItemMapping.Create(item, viewer, AgendaNames.KurinLabel, lookups.GroupNames, lookups.MemberNames, creatorNames, lookups.TargetLabels, lookups.Categories, roster, occ.StartUtc, occ.EndUtc)))
            .Where(r => request.IncludeKurinSchedules || r.Audience != AgendaAudience.Schedule)
            .OrderBy(r => r.StartUtc)
            .ToList();

        return new ServiceResult<IEnumerable<AgendaItemDto>>(ResultType.Success, responses);
    }
}
