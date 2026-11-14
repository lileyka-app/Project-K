using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Events;

/// <summary>
/// The person's answer to one occurrence of an event of any of their kurins, from the dashboard.
/// Refused on an event they could not see from that kurin's calendar — the one visibility rule for
/// both. <paramref name="OccurrenceStartUtc"/> is null for a one-off event.
/// </summary>
public sealed record SetMyEventResponseCommand(Guid AgendaItemKey, DateTime? OccurrenceStartUtc, AgendaRsvpStatus Status) : IRequest<ServiceResult<Unit>>;

public sealed class SetMyEventResponseCommandValidator : AbstractValidator<SetMyEventResponseCommand>
{
    public SetMyEventResponseCommandValidator()
    {
        RuleFor(c => c.AgendaItemKey).NotEmpty();
        RuleFor(c => c.Status).IsInEnum();
    }
}

public sealed class SetMyEventResponseCommandHandler : IRequestHandler<SetMyEventResponseCommand, ServiceResult<Unit>>
{
    private readonly MeAgendaScopes _scopes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;
    private readonly IKraiProfileDirectory _profiles;
    private readonly IAgendaRsvpWriter _rsvps;

    public SetMyEventResponseCommandHandler(MeAgendaScopes scopes, IUnitOfWork unitOfWork, TimeProvider time, IKraiProfileDirectory profiles, IAgendaRsvpWriter rsvps)
    {
        _scopes = scopes;
        _unitOfWork = unitOfWork;
        _time = time;
        _profiles = profiles;
        _rsvps = rsvps;
    }

    public async Task<ServiceResult<Unit>> Handle(SetMyEventResponseCommand request, CancellationToken cancellationToken)
    {
        var item = await _unitOfWork.AgendaItems.GetByKeyWithAssignmentsAsync(request.AgendaItemKey, cancellationToken);
        if (item is null || item.Kind != AgendaItemKind.Event)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, MeErrors.AgendaNotFound, "There is no such event.");
        }

        var context = (await _scopes.BuildAsync(cancellationToken)).FirstOrDefault(c => c.Membership.KurinKey == item.KurinKey);
        if (context is null || !AgendaVisibilityRules.IsVisible(item, context.Viewer.ToScope()))
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, MeErrors.AgendaNotVisible, "You cannot respond to this event.");
        }

        var zone = await _profiles.ForKurinAsync(item.KurinKey, cancellationToken);
        if (!AgendaOccurrenceRules.TryResolveKey(item, request.OccurrenceStartUtc, zone, out var occurrenceKey))
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, MeErrors.AgendaNotOccurrence, "The event has no occurrence starting at that time.");
        }

        await _rsvps.UpsertAsync(item, context.Viewer.ViewerUserKey!.Value, occurrenceKey, request.Status, _time.GetUtcNow().UtcDateTime, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
