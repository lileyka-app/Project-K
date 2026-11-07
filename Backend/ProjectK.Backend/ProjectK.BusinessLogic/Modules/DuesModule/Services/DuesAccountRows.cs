using ProjectK.BusinessLogic.Modules.DuesModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Dues;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// The treasurer's register in one shape for every box: a гурток's accounts and a senior kurin's own.
/// Each row carries the person's quarters, пільги, what the running quarter costs them, and the tags
/// the viewer may see on them, so one screen component filters and exports both.
/// </summary>
public sealed class DuesAccountRows
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKurinTagVisibility _tagVisibility;
    private readonly IDuesKurinShape _shape;

    public DuesAccountRows(IUnitOfWork unitOfWork, IKurinTagVisibility tagVisibility, IDuesKurinShape shape)
    {
        _unitOfWork = unitOfWork;
        _tagVisibility = tagVisibility;
        _shape = shape;
    }

    /// <summary>The kurin's tags and who carries them, cut to what the viewer may see; none with tags off.</summary>
    public async Task<DuesTagLens> TagsAsync(Guid kurinKey, CancellationToken cancellationToken)
    {
        if (!await _shape.TagsOnAsync(kurinKey, cancellationToken))
        {
            return new DuesTagLens([], new Dictionary<Guid, IReadOnlyCollection<Guid>>(), (_, _) => false);
        }

        var shows = await _tagVisibility.ShowsTagsOfAsync(kurinKey, cancellationToken);
        var tags = (await _unitOfWork.KurinTags.GetForKurinAsync(kurinKey, cancellationToken))
            .Select(t => new DuesTagDto { TagKey = t.KurinTagKey, Name = t.Name, ColorHex = t.ColorHex })
            .ToList();
        var byMember = tags.Count == 0
            ? new Dictionary<Guid, IReadOnlyCollection<Guid>>()
            : await _shape.TagsByMemberAsync(kurinKey, cancellationToken);
        return new DuesTagLens(tags, byMember, shows);
    }

    /// <summary>
    /// The rows of one box: <paramref name="groupKey"/> for a гурток, null for the kurin's own. A
    /// membership the kurin no longer has is a removed person, not someone who left: their rows were
    /// closed on removal, and older leftovers must not show as a nameless line.
    /// </summary>
    public static IReadOnlyList<DuesAccountDto> Map(
        IEnumerable<DuesAccount> accounts,
        Guid? groupKey,
        DuesLedger ledger,
        DuesNames names,
        IReadOnlyList<DuesConcessionGrant> grants,
        DuesQuarter current,
        DuesTagLens tags)
    {
        return accounts
            .Where(a => a.GroupKey == groupKey && names.Memberships.ContainsKey(a.MembershipKey))
            .Select(a =>
            {
                var membership = names.Memberships[a.MembershipKey];
                var standing = DuesAccountStandings.Of(
                    hasLeft: membership.LeftAtUtc.HasValue,
                    standsInThisGroup: membership.GroupKey == groupKey);
                var rateTag = ledger.RateTagOf(a.MembershipKey, current);
                var accountKey = names.AccountOf(membership.MemberKey);
                return new DuesAccountDto
                {
                    MembershipKey = a.MembershipKey,
                    MemberKey = membership.MemberKey,
                    FullName = names.OfMembership(a.MembershipKey),
                    Standing = standing,
                    IsConcessionNow = ledger.IsConcession(a.MembershipKey, current),
                    TagKeys = tags.Of(membership.MemberKey, accountKey, membership.GroupKey),
                    RateNow = standing == DuesAccountStanding.Current
                        ? ledger.AmountFor(a.MembershipKey, groupKey, current, rateTag).Total
                        : null,
                    // The tag a quarter is priced by names a tag on the person: hidden where their tags are.
                    RateTagKey = groupKey is null && tags.Shows(accountKey, membership.GroupKey) ? rateTag : null,
                    Grants = grants
                        .Where(g => g.MembershipKey == a.MembershipKey)
                        .OrderByDescending(g => g.FromQuarter)
                        .Select(g => DuesConcessionGrantDto.From(g, current))
                        .ToList(),
                    Quarters = a.Quarters.Select(q => new DuesAccountQuarterDto
                    {
                        Quarter = new QuarterDto { Year = q.Quarter.Year, Number = q.Quarter.Number },
                        Charged = Amount(q.Charged),
                        Paid = Amount(q.Paid),
                        Balance = q.Balance,
                        IsConcession = q.IsConcession,
                        ConcessionName = q.ConcessionName
                    }).ToList(),
                    Charged = a.Charged,
                    Payments = a.Payments,
                    Balance = a.Balance
                };
            })
            .OrderBy(a => a.Standing)
            .ThenBy(a => a.FullName)
            .ToList();
    }

    public static DuesAmountDto Amount(DuesAmount a) =>
        new() { Stanytsia = a.Stanytsia, Krai = a.Krai, Kurin = a.Kurin, Group = a.Group, Total = a.Total };
}

/// <summary>The kurin's tags and the ones on each person, as far as the viewer may see them.</summary>
public sealed class DuesTagLens
{
    private readonly IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> _byMember;
    private readonly Func<Guid?, Guid?, bool> _shows;

    public DuesTagLens(IReadOnlyList<DuesTagDto> tags, IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> byMember, Func<Guid?, Guid?, bool> shows)
    {
        All = tags;
        _byMember = byMember;
        _shows = shows;
    }

    /// <summary>The kurin's tags, for naming and filtering by them.</summary>
    public IReadOnlyList<DuesTagDto> All { get; }

    /// <summary>Whether the viewer may see the tags on a person.</summary>
    public bool Shows(Guid? accountKey, Guid? groupKey) => _shows(accountKey, groupKey);

    public IReadOnlyList<Guid> Of(Guid memberKey, Guid? accountKey, Guid? groupKey) =>
        _shows(accountKey, groupKey) && _byMember.TryGetValue(memberKey, out var keys) ? [.. keys] : [];
}
