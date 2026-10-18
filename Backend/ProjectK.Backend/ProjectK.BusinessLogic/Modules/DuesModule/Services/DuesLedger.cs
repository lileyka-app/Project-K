using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// Works a kurin's dues out from its rows: what each account owes per quarter and what each box holds.
/// Pure, so every money rule lives here and is testable.
/// <list type="bullet">
/// <item>a payment closes the oldest quarter first, and within a quarter the станиця, then the край,
/// then the kurin, then the гурток: money owed to others before the kurin's own;</item>
/// <item>a leftover is a surplus that pays the quarters still to come;</item>
/// <item>money a гурток hands up pays the станиця's part, then the край's, before the kurin's.</item>
/// </list>
/// </summary>
public sealed class DuesLedger
{
    private readonly IReadOnlyList<KurinDuesRate> _kurinRates;
    private readonly ILookup<Guid, GroupDuesRate> _groupRates;
    private readonly ILookup<Guid, DuesConcession> _concessions;
    private readonly IReadOnlyList<DuesCharge> _charges;
    private readonly IReadOnlyList<DuesEntry> _entries;

    public DuesLedger(
        IEnumerable<KurinDuesRate> kurinRates,
        IEnumerable<GroupDuesRate> groupRates,
        IEnumerable<DuesConcession> concessions,
        IEnumerable<DuesCharge> charges,
        IEnumerable<DuesEntry> entries)
    {
        _kurinRates = kurinRates.OrderBy(r => r.FromQuarter).ToList();
        _groupRates = groupRates.OrderBy(r => r.FromQuarter).ToLookup(r => r.GroupKey);
        _concessions = concessions.OrderBy(c => c.FromQuarter).ToLookup(c => c.MembershipKey);
        _charges = charges.ToList();
        _entries = entries.Where(e => !e.IsDeleted).ToList();
    }

    /// <summary>Whether the membership pays the пільгові amounts of the статті in that quarter.</summary>
    public bool IsConcession(Guid membershipKey, DuesQuarter quarter) =>
        _concessions[membershipKey].LastOrDefault(c => c.FromQuarter <= quarter.Index)?.IsConcession ?? false;

    /// <summary>What one quarter costs a membership in a гурток.</summary>
    public DuesAmount AmountFor(Guid membershipKey, Guid groupKey, DuesQuarter quarter)
    {
        var kurin = _kurinRates.LastOrDefault(r => r.FromQuarter <= quarter.Index);
        var group = _groupRates[groupKey].LastOrDefault(r => r.FromQuarter <= quarter.Index);

        decimal stanytsia = 0, krai = 0, own = 0;
        if (kurin is not null)
        {
            var concession = IsConcession(membershipKey, quarter);
            stanytsia = concession ? kurin.StanytsiaReduced : kurin.StanytsiaFull;
            krai = concession ? kurin.KraiReduced : kurin.KraiFull;
            own = concession ? kurin.KurinReduced ?? kurin.KurinShare : kurin.KurinShare;
        }

        return new DuesAmount(stanytsia, own, group?.GroupShare ?? 0, krai);
    }

    /// <summary>Every account in the kurin, one per membership per гурток it was charged or paid in.</summary>
    public IReadOnlyList<DuesAccount> Accounts()
    {
        var payments = _entries
            .Where(e => e.MembershipKey.HasValue && e.GroupKey.HasValue)
            .GroupBy(e => (Membership: e.MembershipKey!.Value, Group: e.GroupKey!.Value))
            .ToDictionary(g => g.Key, g => g.Sum(BalanceEffect));

        var keys = _charges.Select(c => (Membership: c.MembershipKey, Group: c.GroupKey))
            .Concat(payments.Keys)
            .Distinct();

        return keys
            .Select(key => Account(key.Membership, key.Group, payments.GetValueOrDefault(key)))
            .ToList();
    }

    public GroupDuesBox GroupBox(Guid groupKey, IReadOnlyList<DuesAccount>? accounts = null)
    {
        accounts ??= Accounts();
        var entries = _entries.Where(e => e.GroupKey == groupKey).ToList();

        var (cash, card) = Sum(entries.SelectMany(e => CashEffect(e, isKurinBox: false)));
        var allocatedUp = accounts
            .Where(a => a.GroupKey == groupKey)
            .Sum(a => a.Allocated.Up);
        var transfers = entries.Where(e => e.Kind == DuesEntryKind.TransferToKurin).ToList();

        return new GroupDuesBox(
            groupKey,
            new DuesCashBox(cash, card, allocatedUp - transfers.Sum(t => t.Amount)),
            transfers.Where(t => t.ReceivedAtUtc is null).Sum(t => t.Amount));
    }

    /// <summary>The kurin's own box, fed by the transfers it has confirmed.</summary>
    public KurinDuesBox KurinBox(IReadOnlyList<DuesAccount>? accounts = null)
    {
        accounts ??= Accounts();

        var groupKeys = accounts.Select(a => a.GroupKey)
            .Concat(_entries.Where(e => e.GroupKey.HasValue).Select(e => e.GroupKey!.Value))
            .Distinct()
            .ToList();

        var handovers = new List<GroupHandover>();
        decimal receivedForStanytsia = 0, receivedForKrai = 0;
        foreach (var groupKey in groupKeys)
        {
            var transfers = _entries
                .Where(e => e.GroupKey == groupKey && e.Kind == DuesEntryKind.TransferToKurin)
                .ToList();
            var allocated = accounts.Where(a => a.GroupKey == groupKey).Select(a => a.Allocated).ToList();
            var received = transfers.Where(t => t.ReceivedAtUtc.HasValue).Sum(t => t.Amount);

            handovers.Add(new GroupHandover(
                groupKey,
                allocated.Sum(a => a.Up),
                transfers.Sum(t => t.Amount),
                received));

            // What a гурток hands up pays the станиця's part, then the край's, before the kurin's.
            var forStanytsia = Math.Min(received, allocated.Sum(a => a.Stanytsia));
            receivedForStanytsia += forStanytsia;
            receivedForKrai += Math.Min(received - forStanytsia, allocated.Sum(a => a.Krai));
        }

        var moves = _entries
            .Where(e => e.GroupKey is null
                || (e.Kind == DuesEntryKind.TransferToKurin && e.ReceivedAtUtc.HasValue))
            .SelectMany(e => CashEffect(e, isKurinBox: true));
        var (cash, card) = Sum(moves);

        var toStanytsia = receivedForStanytsia - Sent(DuesEntryKind.TransferToStanytsia);
        var toKrai = receivedForKrai - Sent(DuesEntryKind.TransferToKrai);

        return new KurinDuesBox(new DuesCashBox(cash, card, toStanytsia + toKrai), handovers, toStanytsia, toKrai);
    }

    /// <summary>What the kurin's own box has handed on with one kind of transfer.</summary>
    public decimal Sent(DuesEntryKind transfer) =>
        _entries.Where(e => e.GroupKey is null && e.Kind == transfer).Sum(e => e.Amount);

    private DuesAccount Account(Guid membershipKey, Guid groupKey, decimal payments)
    {
        var remaining = Math.Max(payments, 0);
        var quarters = new List<DuesAccountQuarter>();
        foreach (var charge in _charges
            .Where(c => c.MembershipKey == membershipKey && c.GroupKey == groupKey)
            .OrderBy(c => c.Quarter))
        {
            var quarter = DuesQuarter.FromIndex(charge.Quarter);
            var due = AmountFor(membershipKey, groupKey, quarter);
            var stanytsia = Take(ref remaining, due.Stanytsia);
            var krai = Take(ref remaining, due.Krai);
            var kurin = Take(ref remaining, due.Kurin);
            var group = Take(ref remaining, due.Group);
            quarters.Add(new DuesAccountQuarter(
                quarter,
                due,
                new DuesAmount(stanytsia, kurin, group, krai),
                IsConcession(membershipKey, quarter)));
        }

        return new DuesAccount(membershipKey, groupKey, quarters, payments);
    }

    private static decimal Take(ref decimal remaining, decimal due)
    {
        var taken = Math.Min(remaining, due);
        remaining -= taken;
        return taken;
    }

    /// <summary>How an entry moves the person's balance.</summary>
    private static decimal BalanceEffect(DuesEntry entry) => entry.Kind switch
    {
        DuesEntryKind.Contribution => entry.Amount,
        DuesEntryKind.Refund => -entry.Amount,
        DuesEntryKind.Correction => entry.Amount,
        _ => 0
    };

    /// <summary>How an entry moves the cash and card of the box it is read in.</summary>
    private static IEnumerable<(DuesPaymentMethod Method, decimal Delta)> CashEffect(DuesEntry entry, bool isKurinBox)
    {
        switch (entry.Kind)
        {
            case DuesEntryKind.Contribution:
            case DuesEntryKind.OtherIncome:
                yield return (entry.Method, entry.Amount);
                break;
            case DuesEntryKind.Refund:
            case DuesEntryKind.Expense:
            case DuesEntryKind.TransferToStanytsia:
            case DuesEntryKind.TransferToKrai:
                yield return (entry.Method, -entry.Amount);
                break;
            case DuesEntryKind.TransferToKurin:
                // Leaves the гурток's box when handed over; enters the kurin's once confirmed.
                yield return (entry.Method, isKurinBox ? entry.Amount : -entry.Amount);
                break;
            case DuesEntryKind.Exchange:
                yield return (entry.Method, -entry.Amount);
                yield return (entry.CounterMethod ?? entry.Method, entry.Amount);
                break;
        }
    }

    private static (decimal Cash, decimal Card) Sum(IEnumerable<(DuesPaymentMethod Method, decimal Delta)> moves)
    {
        decimal cash = 0, card = 0;
        foreach (var (method, delta) in moves)
        {
            if (method == DuesPaymentMethod.Cash)
            {
                cash += delta;
            }
            else
            {
                card += delta;
            }
        }

        return (cash, card);
    }
}
