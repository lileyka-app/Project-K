using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Events.KurinModule;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Events;

/// <summary>
/// Follows the kurin's structure where the database does not. The changes stay on the request's unit
/// of work and commit with the kurin's own save.
/// </summary>
public sealed class KurinCleanupEventHandler :
    INotificationHandler<DomainEventNotification<KurinDeleted>>,
    INotificationHandler<DomainEventNotification<GroupDissolved>>
{
    private readonly IUnitOfWork _unitOfWork;

    public KurinCleanupEventHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// The people stay; what they earned stays with them. Offices are NO ACTION against both the kurin
    /// and its гуртки, so they go first, and the memberships name the kurin by a foreign key, so they go
    /// with it.
    /// </summary>
    public async Task Handle(DomainEventNotification<KurinDeleted> notification, CancellationToken cancellationToken)
    {
        var kurinKey = notification.Event.KurinKey;
        await _unitOfWork.Leaderships.DeleteForKurinAsync(kurinKey, cancellationToken);
        await _unitOfWork.Memberships.RemoveForKurinAsync(kurinKey, cancellationToken);
    }

    /// <summary>
    /// The гурток's проводи close, its впорядники lead nothing (the revoked rows stay as history), and
    /// its people stay in the kurin without a гурток, which a membership is allowed to be.
    /// </summary>
    public async Task Handle(DomainEventNotification<GroupDissolved> notification, CancellationToken cancellationToken)
    {
        var (_, groupKey, dissolvedAtUtc) = notification.Event;
        await _unitOfWork.Leaderships.CloseForGroupAsync(groupKey, DateOnly.FromDateTime(dissolvedAtUtc), cancellationToken);

        foreach (var assignment in await _unitOfWork.MentorAssignments.GetByGroupKeyAsync(groupKey, cancellationToken))
        {
            assignment.RevokedAtUtc ??= dissolvedAtUtc;
        }

        await _unitOfWork.Memberships.DetachFromGroupAsync(groupKey, cancellationToken);
    }
}
