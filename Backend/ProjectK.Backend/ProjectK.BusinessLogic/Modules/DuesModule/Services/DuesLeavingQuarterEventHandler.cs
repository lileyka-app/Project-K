using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.Events;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// Settles the quarter a person was released in. Paying наперед, that quarter was charged the day it
/// began. Paid in full, it stays: the person bought that quarter and the record of it is kept, even if
/// they leave before it ends. Not paid in full, the charge goes — released, they do not take the
/// quarter, so it is not theirs to pay; whatever part they did pay stays on the account as a surplus,
/// because money is never removed.
/// </summary>
public sealed class DuesLeavingQuarterEventHandler : INotificationHandler<DomainEventNotification<MembershipLeft>>
{
    private readonly IDuesUnitOfWork _dues;
    private readonly DuesLedgerReader _ledgers;
    private readonly IKraiProfileProvider _profiles;

    public DuesLeavingQuarterEventHandler(IDuesUnitOfWork dues, DuesLedgerReader ledgers, IKraiProfileProvider profiles)
    {
        _dues = dues;
        _ledgers = ledgers;
        _profiles = profiles;
    }

    public async Task Handle(DomainEventNotification<MembershipLeft> notification, CancellationToken cancellationToken)
    {
        var left = notification.Event;
        var krai = await _profiles.DuesKraiForKurinAsync(left.KurinKey, cancellationToken);
        var quarter = krai.QuarterOf(left.LeftAtUtc);

        var ledger = await _ledgers.OpenAsync(left.KurinKey, cancellationToken);
        var paidInFull = ledger.Accounts()
            .Where(account => account.MembershipKey == left.MembershipKey)
            .SelectMany(account => account.Quarters)
            .Where(q => q.Quarter == quarter)
            .ToList() is { Count: > 0 } charged && charged.All(q => q.Balance >= 0);
        if (paidInFull)
        {
            return;
        }

        await _dues.DuesCharges.DeleteFromQuarterAsync(left.MembershipKey, quarter.Index, cancellationToken);
    }
}
