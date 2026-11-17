using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Features.Agenda;

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
    private readonly AgendaViewerReader _scopes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;
    private readonly IAgendaRsvpWriter _rsvps;

    public SetMyEventResponseCommandHandler(AgendaViewerReader scopes, IUnitOfWork unitOfWork, TimeProvider time, IAgendaRsvpWriter rsvps)
    {
        _scopes = scopes;
        _unitOfWork = unitOfWork;
        _time = time;
        _rsvps = rsvps;
    }

    public async Task<ServiceResult<Unit>> Handle(SetMyEventResponseCommand request, CancellationToken cancellationToken)
    {
        var viewers = (await _scopes.BuildAsync(cancellationToken)).Select(context => context.Viewer).ToList();
        var outcome = await _rsvps.AnswerAsync(
            viewers, request.AgendaItemKey, request.OccurrenceStartUtc, request.Status, _time.GetUtcNow().UtcDateTime, cancellationToken);

        switch (outcome)
        {
            case AgendaRsvpOutcome.NoSuchEvent:
                return ServiceResult<Unit>.Failure(ResultType.NotFound, AgendaErrors.AgendaNotFound, "There is no such event.");
            case AgendaRsvpOutcome.NotVisible:
                return ServiceResult<Unit>.Failure(ResultType.Forbidden, AgendaErrors.AgendaNotVisible, "You cannot respond to this event.");
            case AgendaRsvpOutcome.NoSuchOccurrence:
                return ServiceResult<Unit>.Failure(ResultType.BadRequest, AgendaErrors.AgendaNotOccurrence, "The event has no occurrence starting at that time.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
