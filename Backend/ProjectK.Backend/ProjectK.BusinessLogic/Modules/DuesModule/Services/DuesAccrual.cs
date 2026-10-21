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
/// Who is charged: a youth membership standing in a гурток, from the quarter it joined — but not
/// before the kurin's first rate — to the quarter it left, or the current one. Впорядники stand in the
/// kurin and in no гурток, so they are never charged. Quarters are read on the край's clock.
/// </remarks>
public sealed class DuesAccrual : IDuesAccrual
{
    private readonly IDuesUnitOfWork _dues;
    private readonly IMembershipDirectory _memberships;
    private readonly TimeProvider _time;
    private readonly IKraiProfileProvider _profiles;

    public DuesAccrual(IDuesUnitOfWork dues, IMembershipDirectory memberships, TimeProvider time, IKraiProfileProvider profiles)
    {
        _dues = dues;
        _memberships = memberships;
        _time = time;
        _profiles = profiles;
    }

    public async Task AccrueKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var firstQuarter = await FirstRatedQuarterAsync(kurinKey, cancellationToken);
        if (firstQuarter is null)
        {
            return;
        }

        var memberships = await _memberships.GetInKurinAsync(kurinKey, cancellationToken);
        var charged = await ChargedAsync(kurinKey, cancellationToken);
        var krai = await _profiles.DuesKraiForKurinAsync(kurinKey, cancellationToken);
        var added = false;
        foreach (var membership in memberships.Where(m => m.Kind == MembershipKind.Youth && m.GroupKey.HasValue))
        {
            added |= Charge(kurinKey, membership, membership.GroupKey!.Value, firstQuarter.Value, charged, krai);
        }

        if (added)
        {
            await SaveAsync(cancellationToken);
        }
    }

    public async Task AccrueMembershipAsync(Guid kurinKey, Guid membershipKey, Guid groupKey, CancellationToken cancellationToken = default)
    {
        var firstQuarter = await FirstRatedQuarterAsync(kurinKey, cancellationToken);
        if (firstQuarter is null)
        {
            return;
        }

        var membership = (await _memberships.GetInKurinAsync(kurinKey, cancellationToken))
            .FirstOrDefault(m => m.MembershipKey == membershipKey && m.Kind == MembershipKind.Youth);
        if (membership is null)
        {
            return;
        }

        var charged = await ChargedAsync(kurinKey, cancellationToken);
        var krai = await _profiles.DuesKraiForKurinAsync(kurinKey, cancellationToken);
        if (Charge(kurinKey, membership, groupKey, firstQuarter.Value, charged, krai))
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

    private bool Charge(
        Guid kurinKey,
        KurinMembershipRecord membership,
        Guid groupKey,
        DuesQuarter firstQuarter,
        HashSet<(Guid Membership, int Quarter)> charged,
        DuesKrai krai)
    {
        var current = krai.QuarterOf(_time.GetUtcNow());
        var from = krai.QuarterOf(membership.JoinedAtUtc);
        if (from < firstQuarter)
        {
            from = firstQuarter;
        }

        var to = membership.LeftAtUtc is { } left && krai.QuarterOf(left) < current ? krai.QuarterOf(left) : current;

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
                    Quarter = quarter.Index
                });
                added = true;
            }
        }

        return added;
    }

    private async Task<DuesQuarter?> FirstRatedQuarterAsync(Guid kurinKey, CancellationToken cancellationToken)
    {
        var rates = await _dues.KurinDuesRates.GetForKurinAsync(kurinKey, cancellationToken);
        return rates.Count == 0 ? null : DuesQuarter.FromIndex(rates.Min(r => r.FromQuarter));
    }

    private async Task<HashSet<(Guid Membership, int Quarter)>> ChargedAsync(Guid kurinKey, CancellationToken cancellationToken)
    {
        var charges = await _dues.DuesCharges.GetForKurinAsync(kurinKey, cancellationToken);
        return charges.Select(c => (c.MembershipKey, c.Quarter)).ToHashSet();
    }
}
