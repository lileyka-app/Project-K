using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Models;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Member.Get;

public sealed record GetMemberDuesQuery(Guid MemberKey) : IRequest<ServiceResult<MemberDuesResponse>>;

/// <summary>
/// A person's вкладка in the kurin the caller acts in. The person may hold several accounts here —
/// one per гурток they were charged in — and the card adds them up.
/// </summary>
public sealed class GetMemberDuesQueryHandler : IRequestHandler<GetMemberDuesQuery, ServiceResult<MemberDuesResponse>>
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
    private readonly IKraiProfileProvider _profiles;

    public GetMemberDuesQueryHandler(
        IDuesUnitOfWork dues,
        DuesLedgerReader ledgers,
        IUnitOfWork unitOfWork,
        IMembershipDirectory memberships,
        IMemberDirectory members,
        IResourceAccessService resourceAccess,
        ICurrentUserContext currentUser,
        TimeProvider time,
        IKraiProfileProvider profiles)
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

    public async Task<ServiceResult<MemberDuesResponse>> Handle(GetMemberDuesQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey is not { } kurinKey)
        {
            return new ServiceResult<MemberDuesResponse>(ResultType.Forbidden);
        }

        var krai = await _profiles.DuesKraiForKurinAsync(kurinKey, cancellationToken);
        var current = krai.QuarterOf(_time.GetUtcNow());
        var memberships = (await _memberships.GetForMemberAsync(request.MemberKey, cancellationToken))
            .Where(m => m.KurinKey == kurinKey)
            .ToList();
        if (memberships.Count == 0)
        {
            return new ServiceResult<MemberDuesResponse>(ResultType.Success,
                new MemberDuesResponse { HasAccount = false, KurinKey = kurinKey, CurrentQuarter = Quarter(current) });
        }

        var ledger = await _ledgers.OpenAsync(kurinKey, cancellationToken);
        var entries = await _dues.DuesEntries.GetForKurinAsync(kurinKey, cancellationToken);
        var groupNames = (await _unitOfWork.Groups.GetAllAsync(kurinKey, cancellationToken))
            .ToDictionary(g => g.GroupKey, g => g.Name);

        var mine = memberships.Select(m => m.MembershipKey).ToHashSet();
        var accounts = ledger.Accounts().Where(a => mine.Contains(a.MembershipKey)).ToList();
        if (accounts.Count == 0)
        {
            return new ServiceResult<MemberDuesResponse>(ResultType.Success,
                new MemberDuesResponse { HasAccount = false, KurinKey = kurinKey, CurrentQuarter = Quarter(current) });
        }

        var standing = memberships.FirstOrDefault(m => m.IsCurrent);
        var currentGroup = standing?.GroupKey;
        var rate = standing is not null && currentGroup is { } g
            ? ledger.AmountFor(standing.MembershipKey, g, current)
            : null;

        var names = await DuesNames.LoadAsync(_memberships, _members, kurinKey, cancellationToken);
        var canOpenGroup = currentGroup is { } groupKey
            && (await _resourceAccess.CheckAccessAsync(ResourceType.GroupDues, ResourceAction.Read, groupKey, cancellationToken)).IsAllowed;

        return new ServiceResult<MemberDuesResponse>(ResultType.Success, new MemberDuesResponse
        {
            HasAccount = true,
            KurinKey = kurinKey,
            Currency = krai.Currency,
            NumberLocale = krai.NumberLocale,
            CurrentQuarter = Quarter(current),
            Balance = accounts.Sum(a => a.Balance),
            QuarterRate = rate is null ? null : Amount(rate),
            IsConcessionNow = standing is not null && ledger.IsConcession(standing.MembershipKey, current),
            CurrentGroupKey = currentGroup,
            CurrentGroupName = currentGroup is { } ck ? groupNames.GetValueOrDefault(ck) : null,
            CanOpenGroupDues = canOpenGroup,
            Accounts = accounts
                .Select(a => new MemberDuesAccountDto
                {
                    GroupKey = a.GroupKey,
                    GroupName = groupNames.GetValueOrDefault(a.GroupKey, "—"),
                    Standing = DuesAccountStandings.Of(hasLeft: standing is null, standsInThisGroup: a.GroupKey == currentGroup),
                    Quarters = a.Quarters.Select(q => new DuesAccountQuarterDto
                    {
                        Quarter = Quarter(q.Quarter),
                        Charged = Amount(q.Charged),
                        Paid = Amount(q.Paid),
                        Balance = q.Balance,
                        IsConcession = q.IsConcession
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
                .ToList()
        });
    }

    private static QuarterDto Quarter(DuesQuarter q) => new() { Year = q.Year, Number = q.Number };

    private static DuesAmountDto Amount(DuesAmount a) =>
        new() { Stanytsia = a.Stanytsia, Krai = a.Krai, Kurin = a.Kurin, Group = a.Group, Total = a.Total };
}
