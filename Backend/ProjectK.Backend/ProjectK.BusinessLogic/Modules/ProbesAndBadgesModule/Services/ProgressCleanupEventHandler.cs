using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Events;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>
/// Clears the progress of people who no longer exist. Nothing in the database cascades it — the rows
/// point at a member by key alone — so the module that owns them listens for the departure instead of
/// being asked by whoever did the removing.
/// </summary>
public sealed class ProgressCleanupEventHandler : INotificationHandler<DomainEventNotification<MembersRemoved>>
{
    private readonly IUnitOfWork _unitOfWork;

    public ProgressCleanupEventHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DomainEventNotification<MembersRemoved> notification, CancellationToken cancellationToken)
    {
        var memberKeys = notification.Event.MemberKeys;
        if (memberKeys.Count == 0)
        {
            return;
        }

        await _unitOfWork.ProbeProgresses.DeleteForMembersAsync(memberKeys, cancellationToken);
        await _unitOfWork.ProbePointProgresses.DeleteForMembersAsync(memberKeys, cancellationToken);
        await _unitOfWork.BadgeProgresses.DeleteForMembersAsync(memberKeys, cancellationToken);
        await _unitOfWork.ProbeProjects.DeleteForMembersAsync(memberKeys, cancellationToken);
    }
}
