using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Models.Events;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// Closes the вкладка of people who are gone. Charges and пільги are plain bookkeeping and go;
/// money is never removed, so an entry of theirs is marked deleted with a trail event, the way a
/// впорядник would delete it. Found live: a youth removed by an administrator stayed in his гурток's
/// table as a nameless row, because nothing in the database ties a charge to a membership.
/// </summary>
public sealed class DuesCleanupEventHandler : INotificationHandler<DomainEventNotification<MembershipsRemoved>>
{
    private readonly IDuesUnitOfWork _dues;
    private readonly TimeProvider _time;

    public DuesCleanupEventHandler(IDuesUnitOfWork dues, TimeProvider time)
    {
        _dues = dues;
        _time = time;
    }

    public async Task Handle(DomainEventNotification<MembershipsRemoved> notification, CancellationToken cancellationToken)
    {
        var keys = notification.Event.MembershipKeys;
        if (keys.Count == 0)
        {
            return;
        }

        await _dues.DuesCharges.DeleteForMembershipsAsync(keys, cancellationToken);
        await _dues.DuesConcessions.DeleteForMembershipsAsync(keys, cancellationToken);
        await _dues.DuesConcessionGrants.DeleteForMembershipsAsync(keys, cancellationToken);

        var now = _time.GetUtcNow().UtcDateTime;
        foreach (var entry in await _dues.DuesEntries.GetForMembershipsAsync(keys, cancellationToken))
        {
            entry.DeletedAtUtc = now;
            DuesEntryTrail.Record(entry, DuesEntryTrail.Deleted, actorUserKey: null, now);
        }
    }
}
