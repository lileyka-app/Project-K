using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.DuesModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.DuesModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Member;

public sealed record GetMemberDuesQuery(Guid MemberKey) : IRequest<ServiceResult<MemberDuesDto>>;

/// <summary>
/// A person's вкладка in the kurin the caller acts in. The person may hold several accounts here —
/// one per гурток they were charged in — and the card adds them up.
/// </summary>
public sealed class GetMemberDuesQueryHandler : IRequestHandler<GetMemberDuesQuery, ServiceResult<MemberDuesDto>>
{
    private const int RecentEntries = 8;

    private readonly IDuesUnitOfWork _dues;
    private readonly DuesLedgerReader _ledgers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMembershipDirectory _memberships;
    private readonly IMemberDirectory _members;
    private readonly IResourceAccessService _resourceAccess;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;
    private readonly IKraiProfileDirectory _profiles;

    public GetMemberDuesQueryHandler(
        IDuesUnitOfWork dues,
        DuesLedgerReader ledgers,
        IUnitOfWork unitOfWork,
        IMembershipDirectory memberships,
        IMemberDirectory members,
        IResourceAccessService resourceAccess,
        ICurrentUserContext currentUser,
        TimeProvider time,
        IKraiProfileDirectory profiles)
    {
        _dues = dues;
        _ledgers = ledgers;
        _unitOfWork = unitOfWork;
        _memberships = memberships;
        _members = members;
        _resourceAccess = resourceAccess;
        _currentUser = currentUser;
        _time = time;
        _profiles = profiles;
    }

    public async Task<ServiceResult<MemberDuesDto>> Handle(GetMemberDuesQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey is not { } kurinKey)
        {
            return DuesErrors.Refused<MemberDuesDto>();
        }

        var krai = await _profiles.DuesKraiForKurinAsync(kurinKey, cancellationToken);
        var current = krai.QuarterOf(_time.GetUtcNow());
        var everywhere = await _memberships.GetForMemberAsync(request.MemberKey, cancellationToken);
        var elsewhere = await ElsewhereAsync(request.MemberKey, kurinKey, everywhere, cancellationToken);
        var memberships = everywhere
            .Where(m => m.KurinKey == kurinKey)
            .ToList();
        if (memberships.Count == 0)
        {
            return new ServiceResult<MemberDuesDto>(ResultType.Success, NoAccount(kurinKey, current, elsewhere));
        }

        var ledger = await _ledgers.OpenAsync(kurinKey, cancellationToken);
        var entries = await _dues.DuesEntries.GetForKurinAsync(kurinKey, cancellationToken);
        var groupNames = (await _unitOfWork.Groups.GetAllAsync(kurinKey, cancellationToken))
            .ToDictionary(g => g.GroupKey, g => g.Name);

        var mine = memberships.Select(m => m.MembershipKey).ToHashSet();
        var accounts = ledger.Accounts().Where(a => mine.Contains(a.MembershipKey)).ToList();
        if (accounts.Count == 0)
        {
            return new ServiceResult<MemberDuesDto>(ResultType.Success, NoAccount(kurinKey, current, elsewhere));
        }

        var standing = memberships.FirstOrDefault(m => m.IsCurrent);
        var currentGroup = standing?.GroupKey;
        // In a kurin without гуртки the person's account is in the kurin's own box.
        var inKurinBox = standing is not null && currentGroup is null
            && accounts.Any(a => a.MembershipKey == standing.MembershipKey && a.GroupKey is null);
        var rate = standing is not null && (currentGroup.HasValue || inKurinBox)
            ? ledger.AmountFor(standing.MembershipKey, currentGroup, current, ledger.RateTagOf(standing.MembershipKey, current))
            : null;

        var names = await DuesNames.LoadAsync(_memberships, _members, kurinKey, cancellationToken);
        var canOpenGroup = currentGroup is { } groupKey
            && (await _resourceAccess.CheckAccessAsync(ResourceType.GroupDues, ResourceAction.Read, groupKey, cancellationToken)).IsAllowed;

        return new ServiceResult<MemberDuesDto>(ResultType.Success, new MemberDuesDto
        {
            HasAccount = true,
            KurinKey = kurinKey,
            Currency = krai.Currency,
            NumberLocale = krai.NumberLocale,
            CurrentQuarter = current.ToDto(),
            Balance = accounts.Sum(a => a.Balance),
            OwedToLeave = accounts.SelectMany(a => a.Quarters).Where(q => q.Quarter < current && q.Balance < 0).Sum(q => -q.Balance),
            QuarterRate = rate?.ToDto(),
            IsConcessionNow = standing is not null && ledger.IsConcession(standing.MembershipKey, current),
            ConcessionNamesNow = standing is null ? [] : ledger.GrantsIn(standing.MembershipKey, current).Select(g => g.Name).ToList(),
            CurrentGroupKey = currentGroup,
            CurrentGroupName = currentGroup is { } ck ? groupNames.GetValueOrDefault(ck) : null,
            CanOpenGroupDues = canOpenGroup,
            Accounts = accounts
                .Select(a => new MemberDuesAccountDto
                {
                    GroupKey = a.GroupKey,
                    GroupName = a.GroupKey is { } ak ? groupNames.GetValueOrDefault(ak, "—") : DuesNames.KurinBox,
                    Standing = DuesAccountStandingRules.Of(hasLeft: standing is null, standsInThisGroup: a.GroupKey == currentGroup),
                    Quarters = a.Quarters.Select(q => new DuesAccountQuarterDto
                    {
                        Quarter = q.Quarter.ToDto(),
                        Charged = q.Charged.ToDto(),
                        Paid = q.Paid.ToDto(),
                        Balance = q.Balance,
                        IsConcession = q.IsConcession,
                        ConcessionName = null
                    }).ToList(),
                    Charged = a.Charged,
                    Payments = a.Payments,
                    Balance = a.Balance
                })
                .OrderBy(a => a.Standing)
                .ToList(),
            Entries = entries
                .Where(e => e.MembershipKey is { } mk && mine.Contains(mk))
                .OrderByDescending(e => e.OccurredOn)
                .ThenByDescending(e => e.CreatedDate)
                .Take(RecentEntries)
                .Select(names.ToDto)
                .ToList(),
            Elsewhere = elsewhere
        });
    }

    /// <summary>
    /// The person's balance in each of their other current kurins, read only for the person themself.
    /// Each kurin counts in its own край's currency.
    /// </summary>
    private async Task<IReadOnlyList<MemberDuesElsewhereDto>> ElsewhereAsync(
        Guid memberKey,
        Guid kurinKey,
        IReadOnlyCollection<MembershipRecord> everywhere,
        CancellationToken cancellationToken)
    {
        var others = everywhere.Where(m => m.IsCurrent && m.KurinKey != kurinKey).ToList();
        if (others.Count == 0
            || _currentUser.UserId is not { } caller
            || await _members.FindAccountKeyAsync(memberKey, cancellationToken) != caller)
        {
            return [];
        }

        var levels = await _members.GetLevelsAsync(memberKey, cancellationToken);
        var result = new List<MemberDuesElsewhereDto>();
        foreach (var kurin in others.GroupBy(m => m.KurinKey))
        {
            var ledger = await _ledgers.OpenAsync(kurin.Key, cancellationToken);
            // Every stretch in that kurin, closed ones too: a debt from before a move is still owed there.
            var mine = everywhere.Where(m => m.KurinKey == kurin.Key).Select(m => m.MembershipKey).ToHashSet();
            var accounts = ledger.Accounts().Where(a => mine.Contains(a.MembershipKey)).ToList();
            if (accounts.Count == 0)
            {
                continue;
            }

            var krai = await _profiles.DuesKraiForKurinAsync(kurin.Key, cancellationToken);
            var line = kurin.First().InLineOf(levels);
            result.Add(new MemberDuesElsewhereDto
            {
                KurinKey = kurin.Key,
                KurinNumber = line.KurinNumber,
                KurinNamedAfter = line.KurinNamedAfter,
                Branch = line.Branch,
                Numbering = line.Numbering,
                Currency = krai.Currency,
                NumberLocale = krai.NumberLocale,
                Balance = accounts.Sum(a => a.Balance)
            });
        }

        return result;
    }

    /// <summary>
    /// The card of someone never charged here. It names no край, so it carries the defaults it always
    /// carried rather than the kurin's currency.
    /// </summary>
    private static MemberDuesDto NoAccount(Guid kurinKey, DuesQuarter current, IReadOnlyList<MemberDuesElsewhereDto> elsewhere) => new()
    {
        HasAccount = false,
        KurinKey = kurinKey,
        Currency = "UAH",
        NumberLocale = "uk-UA",
        CurrentQuarter = current.ToDto(),
        Balance = 0,
        OwedToLeave = 0,
        QuarterRate = null,
        IsConcessionNow = false,
        ConcessionNamesNow = [],
        CurrentGroupKey = null,
        CurrentGroupName = null,
        CanOpenGroupDues = false,
        Accounts = [],
        Entries = [],
        Elsewhere = elsewhere
    };
}
