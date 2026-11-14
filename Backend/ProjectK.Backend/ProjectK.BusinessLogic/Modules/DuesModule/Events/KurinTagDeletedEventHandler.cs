using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Records.DuesModule;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Events;

/// <summary>
/// Moves every unpaid quarter of the kurin's people to the rate their tags give now. A rate by the
/// deleted tag can be neither seen nor changed: the running quarter follows on the next read, older
/// ones would stay on it. Paid quarters and the deleted tag's rates stay, so history reads the same.
/// </summary>
public sealed class KurinTagDeletedEventHandler : INotificationHandler<DomainEventNotification<KurinTagDeleted>>
{
    private readonly IDuesAccrual _accrual;

    public KurinTagDeletedEventHandler(IDuesAccrual accrual)
    {
        _accrual = accrual;
    }

    public Task Handle(DomainEventNotification<KurinTagDeleted> notification, CancellationToken cancellationToken) =>
        _accrual.RepriceKurinAsync(notification.Event.KurinKey, DuesQuarter.FromIndex(0), cancellationToken);
}
