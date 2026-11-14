using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda;

/// <summary>
/// Who answered one occurrence of an event's invitation, with confirmed/waitlist ranking and the
/// caller's own choice. <paramref name="OccurrenceStartUtc"/> is null for a one-off event.
/// </summary>
public sealed record GetAgendaResponsesQuery(Guid AgendaItemKey, DateTime? OccurrenceStartUtc) : IRequest<ServiceResult<AgendaResponsesResponse>>;

public sealed class GetAgendaResponsesQueryHandler : IRequestHandler<GetAgendaResponsesQuery, ServiceResult<AgendaResponsesResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IAgendaAccess _access;
    private readonly ICurrentUserContext _currentUser;
    private readonly IKraiProfileProvider _profiles;

    public GetAgendaResponsesQueryHandler(IUnitOfWork uow, IMemberDirectory members, IAgendaAccess access, ICurrentUserContext currentUser, IKraiProfileProvider profiles)
    {
        _uow = uow;
        _members = members;
        _access = access;
        _currentUser = currentUser;
        _profiles = profiles;
    }

    public async Task<ServiceResult<AgendaResponsesResponse>> Handle(GetAgendaResponsesQuery request, CancellationToken cancellationToken)
    {
        var item = await _uow.AgendaItems.GetByKeyWithAssignmentsAsync(request.AgendaItemKey, cancellationToken);
        if (item is null)
        {
            return ServiceResult<AgendaResponsesResponse>.Failure(ResultType.NotFound, "AGENDA_NOT_FOUND", "Agenda item was not found.");
        }

        if (item.Kind != AgendaItemKind.Event)
        {
            return ServiceResult<AgendaResponsesResponse>.Failure(ResultType.BadRequest, "AGENDA_NOT_EVENT", "Only events accept RSVPs.");
        }

        // Bind to the caller's kurin scope: CanSeeWholeKurin is role-derived, so without this a manager of
        // one kurin could read another kurin's event RSVPs by key.
        if (item.KurinKey != _currentUser.KurinKey)
        {
            return ServiceResult<AgendaResponsesResponse>.Failure(ResultType.Forbidden, "AGENDA_OTHER_KURIN", "Agenda item belongs to a different kurin.");
        }

        var viewer = await _access.BuildViewerAsync(item.KurinKey, cancellationToken);
        if (!AgendaPermissions.IsVisibleTo(item, viewer))
        {
            return ServiceResult<AgendaResponsesResponse>.Failure(ResultType.Forbidden, "AGENDA_NOT_VISIBLE", "You cannot view this event.");
        }

        var zone = await _profiles.ForKurinAsync(item.KurinKey, cancellationToken);
        if (!AgendaOccurrences.TryResolveKey(item, request.OccurrenceStartUtc, zone, out var occurrenceKey))
        {
            return ServiceResult<AgendaResponsesResponse>.Failure(ResultType.BadRequest, "AGENDA_NOT_OCCURRENCE", "The event has no occurrence starting at that time.");
        }

        var responses = await _uow.AgendaResponses.GetForItemAsync(item.AgendaItemKey, occurrenceKey, cancellationToken);
        var names = await ResolveNamesAsync(item.KurinKey, cancellationToken);

        int? capacity = null;
        var waitlistEnabled = false;
        if (item.AgendaCategoryKey.HasValue)
        {
            var category = await _uow.AgendaCategories.GetByKeyAsync(item.AgendaCategoryKey.Value, cancellationToken);
            capacity = category?.Capacity;
            waitlistEnabled = category?.WaitlistEnabled ?? false;
        }

        var picture = AgendaRsvpProjector.Project(item.AgendaItemKey, occurrenceKey, responses, capacity, waitlistEnabled, names, _currentUser.UserId);
        return new ServiceResult<AgendaResponsesResponse>(ResultType.Success, picture);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveNamesAsync(Guid kurinKey, CancellationToken cancellationToken)
    {
        var members = await _members.GetByKurinAsync(kurinKey, cancellationToken);
        return members
            .Where(m => m.UserKey.HasValue && m.UserKey.Value != Guid.Empty)
            .GroupBy(m => m.UserKey!.Value)
            .ToDictionary(g => g.Key, g => $"{g.First().FirstName} {g.First().LastName}".Trim());
    }
}
