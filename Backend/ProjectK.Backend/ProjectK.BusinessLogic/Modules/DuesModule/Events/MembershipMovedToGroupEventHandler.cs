using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Events.KurinModule;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Events;

/// <summary>
/// Charges the quarters a person spent in the гурток they are leaving to that гурток, before the
/// membership forgets it. Without this, the next read would charge them to the new one.
/// </summary>
public sealed class MembershipMovedToGroupEventHandler
    : INotificationHandler<DomainEventNotification<MembershipMovedToGroup>>
{
    private readonly IDuesAccrual _accrual;

    public MembershipMovedToGroupEventHandler(IDuesAccrual accrual)
    {
        _accrual = accrual;
    }

    public Task Handle(DomainEventNotification<MembershipMovedToGroup> notification, CancellationToken cancellationToken)
    {
        var moved = notification.Event;
        return moved.FromGroupKey is { } fromGroupKey && fromGroupKey != moved.ToGroupKey
            ? _accrual.AccrueMembershipAsync(moved.KurinKey, moved.MembershipKey, fromGroupKey, cancellationToken)
            : Task.CompletedTask;
    }
}
