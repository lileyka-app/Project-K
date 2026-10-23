using ProjectK.BusinessLogic.Modules.DuesModule.Models;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// The names a kurin's dues are read with — people by membership, by member and by account — in one
/// read each. The dues module keeps keys only; this is where keys become names, and only for display.
/// </summary>
public sealed class DuesNames
{
    private readonly IReadOnlyDictionary<Guid, KurinMembershipRecord> _memberships;
    private readonly IReadOnlyDictionary<Guid, string> _byMember;
    private readonly IReadOnlyDictionary<Guid, string> _byAccount;

    private DuesNames(
        IReadOnlyDictionary<Guid, KurinMembershipRecord> memberships,
        IReadOnlyDictionary<Guid, string> byMember,
        IReadOnlyDictionary<Guid, string> byAccount,
        IReadOnlyList<DuesPersonDto> people)
    {
        _memberships = memberships;
        _byMember = byMember;
        _byAccount = byAccount;
        People = people;
    }

    /// <summary>Everyone in the kurin, for pickers.</summary>
    public IReadOnlyList<DuesPersonDto> People { get; }

    public IReadOnlyDictionary<Guid, KurinMembershipRecord> Memberships => _memberships;

    public static async Task<DuesNames> LoadAsync(
        IMembershipDirectory memberships,
        IMemberDirectory members,
        Guid kurinKey,
        CancellationToken cancellationToken)
    {
        var inKurin = (await memberships.GetInKurinAsync(kurinKey, cancellationToken)).ToDictionary(m => m.MembershipKey);
        var people = await members.GetByKurinAsync(kurinKey, cancellationToken);

        // The kurin's people are those who stand in it now, but its dues still hold everyone who left
        // or moved on with a debt; read those by key, or their rows in «вибулі з боргом» say «—».
        var current = people.Select(p => p.MemberKey).ToHashSet();
        var leftKeys = inKurin.Values.Select(m => m.MemberKey).Where(key => !current.Contains(key)).Distinct().ToList();
        var left = leftKeys.Count == 0
            ? []
            : await members.GetByKeysAsync(leftKeys, cancellationToken) ?? [];
        var named = people.Concat(left).ToList();

        return new DuesNames(
            inKurin,
            named.GroupBy(p => p.MemberKey).ToDictionary(g => g.Key, g => g.First().FullName),
            named.Where(p => p.UserKey.HasValue).GroupBy(p => p.UserKey!.Value).ToDictionary(g => g.Key, g => g.First().FullName),
            people.OrderBy(p => p.FullName).Select(p => new DuesPersonDto { MemberKey = p.MemberKey, FullName = p.FullName }).ToList());
    }

    public string OfMembership(Guid membershipKey) =>
        _memberships.TryGetValue(membershipKey, out var m) && _byMember.TryGetValue(m.MemberKey, out var name) ? name : "—";

    public string? OfMember(Guid? memberKey) => memberKey is { } key ? _byMember.GetValueOrDefault(key) : null;

    public string? OfAccount(Guid? userKey) => userKey is { } key ? _byAccount.GetValueOrDefault(key) : null;

    public DuesEntryDto ToDto(DuesEntry e) => new()
    {
        DuesEntryKey = e.DuesEntryKey,
        Kind = e.Kind,
        Method = e.Method,
        CounterMethod = e.CounterMethod,
        Amount = e.Amount,
        OccurredOn = e.OccurredOn,
        MembershipKey = e.MembershipKey,
        MemberName = e.MembershipKey is { } mk ? OfMembership(mk) : null,
        CollectedByMemberKey = e.CollectedByMemberKey,
        CollectedByName = OfMember(e.CollectedByMemberKey),
        Note = e.Note,
        IsVerified = e.IsVerified,
        VerifiedAtUtc = e.VerifiedAtUtc,
        VerifiedByName = OfAccount(e.VerifiedByUserKey),
        ReceivedAtUtc = e.ReceivedAtUtc,
        CreatedAtUtc = e.CreatedDate
    };
}
