using MediatR;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Features.Agenda;

/// <summary>
/// The caller's RSVP to one occurrence of an event (йду/не йду/можливо). Upserts one row per
/// (event, occurrence, user); <paramref name="OccurrenceStartUtc"/> is null for a one-off event.
/// </summary>
public sealed record SetAgendaResponseCommand(Guid AgendaItemKey, DateTime? OccurrenceStartUtc, AgendaRsvpStatus Status)
    : IRequest<ServiceResult<AgendaResponsesDto>>;

public sealed class SetAgendaResponseCommandHandler : IRequestHandler<SetAgendaResponseCommand, ServiceResult<AgendaResponsesDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IAgendaAccess _access;
    private readonly ICurrentUserContext _currentUser;
    private readonly IKraiProfileDirectory _profiles;
    private readonly IAgendaRsvpWriter _rsvps;

    public SetAgendaResponseCommandHandler(IUnitOfWork uow, IMemberDirectory members, IAgendaAccess access, ICurrentUserContext currentUser, IKraiProfileDirectory profiles, IAgendaRsvpWriter rsvps)
    {
        _uow = uow;
        _members = members;
        _access = access;
        _currentUser = currentUser;
        _profiles = profiles;
        _rsvps = rsvps;
    }

    public async Task<ServiceResult<AgendaResponsesDto>> Handle(SetAgendaResponseCommand request, CancellationToken cancellationToken)
    {
        var userKey = _currentUser.UserId;
        if (userKey is null)
        {
            return ServiceResult<AgendaResponsesDto>.Failure(ResultType.Unauthorized, AgendaErrors.AgendaNoActor, "Current user could not be resolved.");
        }

        var item = await _uow.AgendaItems.GetByKeyWithAssignmentsAsync(request.AgendaItemKey, cancellationToken);
        if (item is null)
        {
            return AgendaErrors.NotFound<AgendaResponsesDto>();
        }

        if (item.Kind != AgendaItemKind.Event)
        {
            return ServiceResult<AgendaResponsesDto>.Failure(ResultType.BadRequest, AgendaErrors.AgendaNotEvent, "Only events accept RSVPs.");
        }

        // Bind to the caller's kurin scope: CanSeeWholeKurin is role-derived, so without this a manager of
        // one kurin could RSVP to another kurin's event by key.
        if (item.KurinKey != _currentUser.KurinKey)
        {
            return AgendaErrors.OtherKurin<AgendaResponsesDto>();
        }

        var viewer = await _access.BuildViewerAsync(item.KurinKey, cancellationToken);
        if (!AgendaAccess.IsVisibleTo(item, viewer))
        {
            return ServiceResult<AgendaResponsesDto>.Failure(ResultType.Forbidden, AgendaErrors.AgendaNotVisible, "You cannot respond to this event.");
        }

        var zone = await _profiles.ForKurinAsync(item.KurinKey, cancellationToken);
        if (!AgendaOccurrenceRules.TryResolveKey(item, request.OccurrenceStartUtc, zone, out var occurrenceKey))
        {
            return ServiceResult<AgendaResponsesDto>.Failure(ResultType.BadRequest, AgendaErrors.AgendaNotOccurrence, "The event has no occurrence starting at that time.");
        }

        await _rsvps.UpsertAsync(item, userKey.Value, occurrenceKey, request.Status, DateTime.UtcNow, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var responses = await _uow.AgendaResponses.GetForItemAsync(item.AgendaItemKey, occurrenceKey, cancellationToken);
        var names = AgendaCreatorNames.FromMembers(await _members.GetByKurinAsync(item.KurinKey, cancellationToken));

        int? capacity = null;
        var waitlistEnabled = false;
        if (item.AgendaCategoryKey.HasValue)
        {
            var category = await _uow.AgendaCategories.GetByKeyAsync(item.AgendaCategoryKey.Value, cancellationToken);
            capacity = category?.Capacity;
            waitlistEnabled = category?.WaitlistEnabled ?? false;
        }

        var picture = AgendaRsvpRules.Project(item.AgendaItemKey, occurrenceKey, responses, capacity, waitlistEnabled, names, userKey);
        return new ServiceResult<AgendaResponsesDto>(ResultType.Success, picture);
    }
}
