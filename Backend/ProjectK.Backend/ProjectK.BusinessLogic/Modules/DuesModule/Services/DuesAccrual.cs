using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Exceptions;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <inheritdoc />
/// <remarks>
/// Who is charged: a youth membership standing in a гурток — or, in a senior kurin, which has no
/// гуртки, every membership of any kind in the kurin's own box, провід too — from the quarter it
/// joined, but not before the kurin's first rate, up to the last quarter that has fallen due. Paying
/// наперед, that is the running quarter; paying за пройдений квартал, the one before it. The quarter a
/// person left in is not charged here: released at the start of a quarter, they do not take it (a
/// charge made for it while they still stood, and paid, is kept — see DuesLeavingQuarterEventHandler),
/// and the kurin they move to starts from the next quarter instead. Впорядники stand in the kurin and
/// in no гурток, so a УПЮ kurin never charges them. Quarters are read on the край's clock.
/// <para>
/// In a senior kurin each quarter is priced at the highest rate among the person's tags. A quarter not
/// paid in full follows the tags and rates as they are now: the last quarter fallen due is repriced on
/// every read, so a tag put on or taken off moves the running charge, and a rate set from a quarter
/// reprices every unpaid quarter from it. A quarter paid in full keeps the tag it was paid at, and so
/// does any quarter of someone who has left: their tags no longer count, their debt stays what it was.
/// </para>
/// </remarks>
public sealed class DuesAccrual : IDuesAccrual
{
    private readonly IDuesUnitOfWork _dues;
    private readonly IMembershipDirectory _memberships;
    private readonly TimeProvider _time;
    private readonly IKraiProfileProvider _profiles;
    private readonly IDuesKurinShape _shape;

    public DuesAccrual(IDuesUnitOfWork dues, IMembershipDirectory memberships, TimeProvider time, IKraiProfileProvider profiles, IDuesKurinShape shape)
    {
        _dues = dues;
        _memberships = memberships;
        _time = time;
        _profiles = profiles;
        _shape = shape;
    }

    public async Task AccrueKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var rates = await _dues.KurinDuesRates.GetForKurinAsync(kurinKey, cancellationToken);
        if (rates.Count == 0)
        {
            return;
        }

        var memberships = await _memberships.GetInKurinAsync(kurinKey, cancellationToken);
        var pricing = await PricingAsync(kurinKey, rates, cancellationToken);
        var charged = await ChargedAsync(kurinKey, cancellationToken);
        var krai = await _profiles.DuesKraiForKurinAsync(kurinKey, cancellationToken);
        var last = await LastDueQuarterAsync(kurinKey, krai, cancellationToken);

        var chargeable = memberships.Where(m => IsCharged(m, m.GroupKey, pricing)).ToList();
        var keptElsewhere = await ArrivalsKeptElsewhereAsync(kurinKey, chargeable, pricing, last, charged, krai, cancellationToken);

        var added = false;
        foreach (var membership in chargeable)
        {
            added |= Charge(kurinKey, membership, membership.GroupKey, pricing, last, charged, krai, keptElsewhere);
        }

        if (added)
        {
            await SaveAsync(cancellationToken);
        }

        if (pricing.IsSenior && await RepriceAsync(kurinKey, pricing, memberships, last, cancellationToken))
        {
            await SaveAsync(cancellationToken);
        }
    }

    public async Task AccrueMembershipAsync(Guid kurinKey, Guid membershipKey, Guid groupKey, CancellationToken cancellationToken = default)
    {
        var rates = await _dues.KurinDuesRates.GetForKurinAsync(kurinKey, cancellationToken);
        if (rates.Count == 0)
        {
            return;
        }

        var pricing = await PricingAsync(kurinKey, rates, cancellationToken);
        var membership = (await _memberships.GetInKurinAsync(kurinKey, cancellationToken))
            .FirstOrDefault(m => m.MembershipKey == membershipKey && IsCharged(m, groupKey, pricing));
        if (membership is null)
        {
            return;
        }

        var charged = await ChargedAsync(kurinKey, cancellationToken);
        var krai = await _profiles.DuesKraiForKurinAsync(kurinKey, cancellationToken);
        var last = await LastDueQuarterAsync(kurinKey, krai, cancellationToken);
        var keptElsewhere = await ArrivalsKeptElsewhereAsync(kurinKey, [membership], pricing, last, charged, krai, cancellationToken);
        if (Charge(kurinKey, membership, groupKey, pricing, last, charged, krai, keptElsewhere))
        {
            await SaveAsync(cancellationToken);
        }
    }

    public async Task RepriceKurinAsync(Guid kurinKey, DuesQuarter from, CancellationToken cancellationToken = default)
    {
        var rates = await _dues.KurinDuesRates.GetForKurinAsync(kurinKey, cancellationToken);
        if (rates.Count == 0)
        {
            return;
        }

        var pricing = await PricingAsync(kurinKey, rates, cancellationToken);
        if (!pricing.IsSenior)
        {
            return;
        }

        var memberships = await _memberships.GetInKurinAsync(kurinKey, cancellationToken);
        if (await RepriceAsync(kurinKey, pricing, memberships, from, cancellationToken))
        {
            await SaveAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Two reads of the same kurin accrue at once — the dashboard asks for dues and points together —
    /// and the slower one hits the unique (membership, quarter) index. The charges it wanted are the
    /// ones the faster read just wrote, so there is nothing to report; anything still missing is
    /// charged by the next read.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dues.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateRowException)
        {
        }
    }

    /// <summary>A УПЮ kurin charges its youth in гуртки; a senior kurin charges everyone in it.</summary>
    private static bool IsCharged(KurinMembershipRecord membership, Guid? groupKey, Pricing pricing) =>
        pricing.IsSenior || (membership.Kind == MembershipKind.Youth && groupKey.HasValue);

    private bool Charge(
        Guid kurinKey,
        KurinMembershipRecord membership,
        Guid? groupKey,
        Pricing pricing,
        DuesQuarter last,
        HashSet<(Guid Membership, int Quarter)> charged,
        DuesKrai krai,
        IReadOnlySet<Guid> keptElsewhere)
    {
        var from = krai.QuarterOf(membership.JoinedAtUtc);
        if (keptElsewhere.Contains(membership.MembershipKey))
        {
            from = from.Next();
        }

        if (from < pricing.FirstQuarter)
        {
            from = pricing.FirstQuarter;
        }

        var to = last;
        if (membership.LeftAtUtc is { } left && krai.QuarterOf(left) <= to)
        {
            to = DuesQuarter.FromIndex(krai.QuarterOf(left).Index - 1);
        }

        var tags = pricing.Tags.GetValueOrDefault(membership.MemberKey) ?? [];
        var added = false;
        for (var quarter = from; quarter <= to; quarter = quarter.Next())
        {
            if (charged.Add((membership.MembershipKey, quarter.Index)))
            {
                _dues.DuesCharges.Create(new DuesCharge
                {
                    KurinKey = kurinKey,
                    GroupKey = groupKey,
                    MembershipKey = membership.MembershipKey,
                    Quarter = quarter.Index,
                    RateTagKey = pricing.IsSenior ? DuesLedger.RateTagFor(pricing.Rates, tags, quarter) : null
                });
                added = true;
            }
        }

        return added;
    }

    /// <summary>
    /// The memberships whose first quarter here the kurin they came from keeps: they left it in the
    /// quarter they joined this one, and its charge for that quarter stands — paid in full, since an
    /// unpaid one goes on release. Charging it here too would make them pay the quarter twice.
    /// </summary>
    private async Task<IReadOnlySet<Guid>> ArrivalsKeptElsewhereAsync(
        Guid kurinKey,
        IEnumerable<KurinMembershipRecord> memberships,
        Pricing pricing,
        DuesQuarter last,
        HashSet<(Guid Membership, int Quarter)> charged,
        DuesKrai krai,
        CancellationToken cancellationToken)
    {
        var arriving = memberships
            .Select(m => (Membership: m, Quarter: krai.QuarterOf(m.JoinedAtUtc)))
            .Where(a => a.Quarter >= pricing.FirstQuarter && a.Quarter <= last
                && !charged.Contains((a.Membership.MembershipKey, a.Quarter.Index)))
            .ToList();
        if (arriving.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var history = await _memberships.GetForMembersAsync(arriving.Select(a => a.Membership.MemberKey).Distinct().ToList(), cancellationToken);
        var came = new List<(Guid Arriving, Guid Left, int Quarter)>();
        foreach (var (membership, quarter) in arriving)
        {
            foreach (var before in history.GetValueOrDefault(membership.MemberKey) ?? [])
            {
                if (before.KurinKey != kurinKey && before.LeftAtUtc is { } leftAt && krai.QuarterOf(leftAt) == quarter)
                {
                    came.Add((membership.MembershipKey, before.MembershipKey, quarter.Index));
                }
            }
        }

        if (came.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var kept = (await _dues.DuesCharges.GetForMembershipsAsync(came.Select(c => c.Left).Distinct().ToList(), cancellationToken))
            .Select(c => (c.MembershipKey, c.Quarter))
            .ToHashSet();
        return came.Where(c => kept.Contains((c.Left, c.Quarter))).Select(c => c.Arriving).ToHashSet();
    }

    /// <summary>
    /// Sets the tag of every charge from <paramref name="from"/> on that is not paid in full to the one
    /// the person's tags and the kurin's rates give now. Whether a quarter is paid is read before any
    /// of them changes. True when a charge changed.
    /// </summary>
    private async Task<bool> RepriceAsync(
        Guid kurinKey,
        Pricing pricing,
        IReadOnlyCollection<KurinMembershipRecord> memberships,
        DuesQuarter from,
        CancellationToken cancellationToken)
    {
        // Whoever has left stays out: their tags are no longer read, and that must not move their debt.
        var members = memberships.Where(m => m.LeftAtUtc is null).ToDictionary(m => m.MembershipKey, m => m.MemberKey);
        Guid? TagNow(DuesCharge charge) => DuesLedger.RateTagFor(
            pricing.Rates,
            pricing.Tags.GetValueOrDefault(members[charge.MembershipKey]) ?? [],
            DuesQuarter.FromIndex(charge.Quarter));

        // Nearly every read finds nothing to change: the whole ledger is built only when something would.
        var charges = await _dues.DuesCharges.GetForKurinAsync(kurinKey, cancellationToken);
        if (!charges.Any(c => c.Quarter >= from.Index && members.ContainsKey(c.MembershipKey) && TagNow(c) != c.RateTagKey))
        {
            return false;
        }

        var ledger = new DuesLedger(
            pricing.Rates,
            await _dues.GroupDuesRates.GetForKurinAsync(kurinKey, cancellationToken),
            await _dues.DuesConcessions.GetForKurinAsync(kurinKey, cancellationToken),
            charges,
            await _dues.DuesEntries.GetForKurinAsync(kurinKey, cancellationToken),
            await _dues.DuesConcessionGrants.GetForKurinAsync(kurinKey, cancellationToken));
        var unpaid = ledger.Accounts()
            .SelectMany(a => a.Quarters.Where(q => q.Balance < 0).Select(q => (a.MembershipKey, q.Quarter.Index)))
            .ToHashSet();

        var changed = false;
        var tracked = await _dues.DuesCharges.GetTrackedFromQuarterAsync(kurinKey, from.Index, cancellationToken);
        foreach (var charge in tracked.Where(c => members.ContainsKey(c.MembershipKey) && unpaid.Contains((c.MembershipKey, c.Quarter))))
        {
            var tag = TagNow(charge);
            if (tag != charge.RateTagKey)
            {
                charge.RateTagKey = tag;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>The kurin's rates and, in a senior kurin, its people's tags: what a new charge is priced by.</summary>
    private async Task<Pricing> PricingAsync(Guid kurinKey, IReadOnlyCollection<KurinDuesRate> rates, CancellationToken cancellationToken)
    {
        var isSenior = await _shape.IsSeniorAsync(kurinKey, cancellationToken);
        var tags = isSenior
            ? await _shape.TagsByMemberAsync(kurinKey, cancellationToken)
            : new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        return new Pricing(DuesQuarter.FromIndex(rates.Min(r => r.FromQuarter)), rates, isSenior, tags);
    }

    private async Task<HashSet<(Guid Membership, int Quarter)>> ChargedAsync(Guid kurinKey, CancellationToken cancellationToken)
    {
        var charges = await _dues.DuesCharges.GetForKurinAsync(kurinKey, cancellationToken);
        return charges.Select(c => (c.MembershipKey, c.Quarter)).ToHashSet();
    }

    /// <summary>The last quarter that has fallen due today, by the kurin's choice of when it pays.</summary>
    private async Task<DuesQuarter> LastDueQuarterAsync(Guid kurinKey, DuesKrai krai, CancellationToken cancellationToken)
    {
        var current = krai.QuarterOf(_time.GetUtcNow());
        var settings = await _dues.KurinDuesSettings.GetForKurinAsync(kurinKey, cancellationToken);
        return settings?.ChargeTiming == DuesChargeTiming.InArrears
            ? DuesQuarter.FromIndex(current.Index - 1)
            : current;
    }

    private sealed record Pricing(
        DuesQuarter FirstQuarter,
        IReadOnlyCollection<KurinDuesRate> Rates,
        bool IsSenior,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> Tags);
}
