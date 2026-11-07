using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Get;

/// <summary>
/// Calendar feed: dated items in the window that the current user may see. With
/// <see cref="IncludeKurinSchedules"/> («Графіки гуртків») it also carries every event of a group
/// marked «графік куреня», as <see cref="AgendaAudience.Schedule"/>; without it those are left out
/// for everyone, whole-kurin viewers too, so the switch means the same to all.
/// </summary>
public sealed record GetAgendaItemsQuery(Guid KurinKey, DateTime? FromUtc, DateTime? ToUtc, bool IncludeKurinSchedules = false)
    : IRequest<ServiceResult<IEnumerable<AgendaItemResponse>>>;

public sealed class GetAgendaItemsQueryHandler
    : IRequestHandler<GetAgendaItemsQuery, ServiceResult<IEnumerable<AgendaItemResponse>>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IAgendaAccess _access;
    private readonly UserManager<AppUser> _userManager;
    private readonly TimeProvider _timeProvider;
    private readonly IKraiProfileProvider _profiles;

    public GetAgendaItemsQueryHandler(IUnitOfWork uow,
        IMemberDirectory members, IAgendaAccess access, UserManager<AppUser> userManager, TimeProvider timeProvider,
        IKraiProfileProvider profiles)
    {
        _uow = uow;
        _members = members;
        _access = access;
        _userManager = userManager;
        _timeProvider = timeProvider;
        _profiles = profiles;
    }

    public async Task<ServiceResult<IEnumerable<AgendaItemResponse>>> Handle(GetAgendaItemsQuery request, CancellationToken cancellationToken)
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

        var lookups = await AgendaLookups.LoadAsync(_uow, _members, request.KurinKey, items, cancellationToken);
        var creatorNames = await AgendaCreatorNames.ResolveAsync(_userManager, lookups.CreatorNames, items, cancellationToken);
        var roster = await AgendaRoster.LoadAsync(_uow, lookups.MemberGroups, items, cancellationToken);

        // Recurring items are expanded into one row per occurrence inside the query window; one-offs pass
        // through unchanged. A missing window is bounded so an open-ended series can't expand forever.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var windowFrom = request.FromUtc ?? now.AddMonths(-6);
        var windowTo = request.ToUtc ?? now.AddMonths(12);
        var zone = await _profiles.ForKurinAsync(request.KurinKey, cancellationToken);

        var responses = items
            .SelectMany(item => item.RecurrenceFrequency == RecurrenceFrequency.None
                ? new[] { AgendaItemResponseFactory.Create(item, viewer, AgendaLookups.KurinLabel, lookups.GroupNames, lookups.MemberNames, creatorNames, lookups.TargetLabels, lookups.Categories, roster) }
                : AgendaRecurrence.Expand(item, windowFrom, windowTo, zone)
                    .Select(occ => AgendaItemResponseFactory.Create(item, viewer, AgendaLookups.KurinLabel, lookups.GroupNames, lookups.MemberNames, creatorNames, lookups.TargetLabels, lookups.Categories, roster, occ.StartUtc, occ.EndUtc)))
            .Where(r => request.IncludeKurinSchedules || r.Audience != AgendaAudience.Schedule)
            .OrderBy(r => r.StartUtc)
            .ToList();

        return new ServiceResult<IEnumerable<AgendaItemResponse>>(ResultType.Success, responses);
    }
}
