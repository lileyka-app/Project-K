using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Events.KurinModule;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Events;

/// <summary>
/// Writes the move down before the membership forgets the old гурток, so the points earned there stay
/// there. Not saved here: the move commits with the command that made it, or not at all.
/// </summary>
public sealed class RecordScoreGroupMoveEventHandler
    : INotificationHandler<DomainEventNotification<MembershipMovedToGroup>>
{
    private readonly IScoreUnitOfWork _score;
    private readonly TimeProvider _time;

    public RecordScoreGroupMoveEventHandler(IScoreUnitOfWork score, TimeProvider time)
    {
        _score = score;
        _time = time;
    }

    public Task Handle(DomainEventNotification<MembershipMovedToGroup> notification, CancellationToken cancellationToken)
    {
        var moved = notification.Event;
        if (moved.FromGroupKey != moved.ToGroupKey)
        {
            _score.ScoreGroupMoves.Create(new ScoreGroupMove
            {
                KurinKey = moved.KurinKey,
                MembershipKey = moved.MembershipKey,
                FromGroupKey = moved.FromGroupKey,
                ToGroupKey = moved.ToGroupKey,
                MovedAtUtc = _time.GetUtcNow().UtcDateTime
            }, cancellationToken);
        }

        return Task.CompletedTask;
    }
}
