using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.DuesModule;
using ProjectK.Common.Enums.DuesModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.DuesModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin;

public sealed record GetKurinDuesQuery(Guid KurinKey) : IRequest<ServiceResult<KurinDuesDto>>;

/// <summary>The kurin's box in one read, charges brought up to date first.</summary>
public sealed class GetKurinDuesQueryHandler : IRequestHandler<GetKurinDuesQuery, ServiceResult<KurinDuesDto>>
{
    private readonly DuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDuesAccrual _accrual;
    private readonly IMembershipDirectory _memberships;
    private readonly IMemberDirectory _members;
    private readonly IResourceAccessService _resourceAccess;
    private readonly TimeProvider _time;
    private readonly IKraiProfileDirectory _profiles;
    private readonly IDuesKurinShape _shape;
    private readonly DuesAccountRows _rows;

    public GetKurinDuesQueryHandler(
        DuesAccess access,
        IDuesUnitOfWork dues,
        IUnitOfWork unitOfWork,
        IDuesAccrual accrual,
        IMembershipDirectory memberships,
        IMemberDirectory members,
        IResourceAccessService resourceAccess,
        TimeProvider time,
        IKraiProfileDirectory profiles,
        IDuesKurinShape shape,
        DuesAccountRows rows)
    {
        _access = access;
        _dues = dues;
        _unitOfWork = unitOfWork;
        _accrual = accrual;
        _memberships = memberships;
        _members = members;
        _resourceAccess = resourceAccess;
        _time = time;
        _profiles = profiles;
        _shape = shape;
        _rows = rows;
    }

    public async Task<ServiceResult<KurinDuesDto>> Handle(GetKurinDuesQuery request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<KurinDuesDto>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var kurinKey = request.KurinKey;
        await _accrual.AccrueKurinAsync(kurinKey, cancellationToken);

        var rates = await _dues.KurinDuesRates.GetForKurinAsync(kurinKey, cancellationToken);
        var groupRates = await _dues.GroupDuesRates.GetForKurinAsync(kurinKey, cancellationToken);
        var concessions = await _dues.DuesConcessions.GetForKurinAsync(kurinKey, cancellationToken);
        var charges = await _dues.DuesCharges.GetForKurinAsync(kurinKey, cancellationToken);
        var entries = await _dues.DuesEntries.GetForKurinAsync(kurinKey, cancellationToken);
        var grants = await _dues.DuesConcessionGrants.GetForKurinAsync(kurinKey, cancellationToken);
        var names = await DuesNames.LoadAsync(_memberships, _members, kurinKey, cancellationToken);
        var groupNames = (await _unitOfWork.Groups.GetAllAsync(kurinKey, cancellationToken))
            .ToDictionary(g => g.GroupKey, g => g.Name);

        var ledger = new DuesLedger(rates, groupRates, concessions, charges, entries, grants);
        // Memberships the kurin no longer has are removed people; see GetGroupDuesQuery.
        var accounts = ledger.Accounts().Where(a => names.Memberships.ContainsKey(a.MembershipKey)).ToList();
        var kurinBox = ledger.KurinBox(accounts);
        var krai = await _profiles.DuesKraiForKurinAsync(kurinKey, cancellationToken);
        var current = krai.QuarterOf(_time.GetUtcNow());
        var isSenior = await _shape.IsSeniorAsync(kurinKey, cancellationToken);
        var tags = await _rows.TagsAsync(kurinKey, cancellationToken);

        var transfers = entries
            .Where(e => e.GroupKey.HasValue && e.Kind == DuesEntryKind.TransferToKurin)
            .OrderByDescending(e => e.OccurredOn)
            .ThenByDescending(e => e.CreatedDate)
            .Select(e => new DuesTransferDto
            {
                DuesEntryKey = e.DuesEntryKey,
                GroupKey = e.GroupKey!.Value,
                GroupName = groupNames.GetValueOrDefault(e.GroupKey.Value, "—"),
                Amount = e.Amount,
                Method = e.Method,
                OccurredOn = e.OccurredOn,
                CollectedByName = names.OfMember(e.CollectedByMemberKey),
                Note = e.Note,
                IsReceived = e.ReceivedAtUtc.HasValue,
                ReceivedAtUtc = e.ReceivedAtUtc,
                ReceivedByName = names.OfAccount(e.ReceivedByUserKey)
            })
            .ToList();

        var groups = kurinBox.Handovers
            .Select(h => new KurinGroupHandoverDto
            {
                GroupKey = h.GroupKey,
                GroupName = groupNames.GetValueOrDefault(h.GroupKey, "—"),
                OwedUp = h.OwedUp,
                Transferred = h.Transferred,
                Received = h.Received,
                Outstanding = h.Outstanding,
                InTransit = h.Transferred - h.Received
            })
            .OrderBy(g => g.GroupName)
            .ToList();

        var canKeep = await _resourceAccess.CheckAccessAsync(ResourceType.KurinDues, ResourceAction.Create, kurinKey, cancellationToken);
        var canVerify = await _resourceAccess.CheckAccessAsync(ResourceType.KurinDues, ResourceAction.Manage, kurinKey, cancellationToken);
        var canSetRates = await _resourceAccess.CheckAccessAsync(ResourceType.KurinDues, ResourceAction.Update, kurinKey, cancellationToken);

        var first = entries.Select(e => DuesQuarter.Of(e.OccurredOn).Index)
            .Concat(charges.Select(c => c.Quarter))
            .DefaultIfEmpty(current.Index)
            .Min();

        return new ServiceResult<KurinDuesDto>(ResultType.Success, new KurinDuesDto
        {
            KurinKey = kurinKey,
            Currency = krai.Currency,
            NumberLocale = krai.NumberLocale,
            Upstream = krai.Dues.Upstream,
            CurrentQuarter = current.ToDto(),
            ChargeTiming = (await _dues.KurinDuesSettings.GetForKurinAsync(kurinKey, cancellationToken))?.ChargeTiming ?? DuesChargeTiming.InAdvance,
            Years = DuesMapping.Years(DuesQuarter.FromIndex(first), current),
            // With tags off, everyone pays the rate for everyone: the tags' rates stand idle and are not shown.
            Rates = rates.Where(r => r.TagKey is null || tags.All.Count > 0).Select(r => r.ToDto()).ToList(),
            Box = new DuesBoxDto
            {
                Cash = kurinBox.Box.Cash,
                Card = kurinBox.Box.Card,
                Total = kurinBox.Box.Total,
                ToForward = kurinBox.Box.ToForward,
                Ahead = kurinBox.Box.Ahead,
                Own = kurinBox.Box.Own,
                InTransit = groups.Sum(g => g.InTransit)
            },
            ToStanytsia = kurinBox.ToStanytsia,
            ToKrai = kurinBox.ToKrai,
            SentToStanytsia = ledger.Sent(DuesEntryKind.TransferToStanytsia),
            SentToKrai = ledger.Sent(DuesEntryKind.TransferToKrai),
            Groups = groups,
            Quarters = Quarters(accounts.Where(a => a.GroupKey.HasValue).ToList(), groupNames),
            IsSenior = isSenior,
            Accounts = DuesAccountRows.Map(accounts, null, ledger, names, grants, current, tags),
            Tags = tags.All,
            KraiConcessions = krai.Dues.Concessions.Select(r => r.ToDto()).ToList(),
            Transfers = transfers,
            Entries = entries
                .Where(e => e.GroupKey is null)
                .OrderByDescending(e => e.OccurredOn)
                .ThenByDescending(e => e.CreatedDate)
                .Select(names.ToDto)
                .ToList(),
            People = names.People,
            Viewer = new KurinDuesViewerDto { CanKeep = canKeep.IsAllowed, CanVerify = canVerify.IsAllowed, CanSetRates = canSetRates.IsAllowed }
        });
    }

    /// <summary>
    /// The гуртки's quarterly tables folded into the kurin's: per quarter, per гурток, how many youth
    /// were charged, what they owe up and what of it is paid — and the станиця's and край's parts on their own.
    /// </summary>
    private static IReadOnlyList<KurinDuesQuarterDto> Quarters(IReadOnlyList<DuesAccount> accounts, IReadOnlyDictionary<Guid, string> groupNames)
    {
        var cells = accounts
            .SelectMany(a => a.Quarters.Select(q => (GroupKey: a.GroupKey!.Value, q)))
            .GroupBy(x => x.q.Quarter.Index)
            .OrderBy(g => g.Key);

        var result = new List<KurinDuesQuarterDto>();
        foreach (var quarter in cells)
        {
            var groups = quarter
                .GroupBy(x => x.GroupKey)
                .Select(g => Row(g.Key, groupNames.GetValueOrDefault(g.Key, "—"), g.Select(x => x.q).ToList()))
                .OrderBy(r => r.GroupName)
                .ToList();
            result.Add(new KurinDuesQuarterDto
            {
                Quarter = DuesQuarter.FromIndex(quarter.Key).ToDto(),
                Groups = groups,
                Total = Row(Guid.Empty, "Разом", quarter.Select(x => x.q).ToList())
            });
        }

        return result;
    }

    private static KurinDuesQuarterGroupDto Row(Guid groupKey, string name, IReadOnlyList<DuesAccountQuarter> quarters)
    {
        var expected = quarters.Sum(q => q.Charged.Up);
        var collected = quarters.Sum(q => q.Paid.Up);
        return new KurinDuesQuarterGroupDto
        {
            GroupKey = groupKey,
            GroupName = name,
            YouthCount = quarters.Count,
            ExpectedUp = expected,
            CollectedUp = collected,
            DebtUp = expected - collected,
            StanytsiaExpected = quarters.Sum(q => q.Charged.Stanytsia),
            StanytsiaCollected = quarters.Sum(q => q.Paid.Stanytsia),
            KraiExpected = quarters.Sum(q => q.Charged.Krai),
            KraiCollected = quarters.Sum(q => q.Paid.Krai)
        };
    }
}
