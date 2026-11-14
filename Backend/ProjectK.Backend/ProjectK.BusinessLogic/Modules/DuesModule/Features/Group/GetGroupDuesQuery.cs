using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Dtos.DuesModule;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.DuesModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Group;

public sealed record GetGroupDuesQuery(Guid GroupKey) : IRequest<ServiceResult<GroupDuesDto>>;

/// <summary>
/// A гурток's whole box in one read. Charges are brought up to date first, so a quarter that began
/// since the last visit is already on the table.
/// </summary>
public sealed class GetGroupDuesQueryHandler : IRequestHandler<GetGroupDuesQuery, ServiceResult<GroupDuesDto>>
{
    private readonly GroupDuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly IDuesAccrual _accrual;
    private readonly IMembershipDirectory _memberships;
    private readonly IMemberDirectory _members;
    private readonly IResourceAccessService _resourceAccess;
    private readonly TimeProvider _time;
    private readonly IKraiProfileDirectory _profiles;
    private readonly DuesAccountRows _rows;

    public GetGroupDuesQueryHandler(
        GroupDuesAccess access,
        IDuesUnitOfWork dues,
        IDuesAccrual accrual,
        IMembershipDirectory memberships,
        IMemberDirectory members,
        IResourceAccessService resourceAccess,
        TimeProvider time,
        IKraiProfileDirectory profiles,
        DuesAccountRows rows)
    {
        _access = access;
        _dues = dues;
        _accrual = accrual;
        _memberships = memberships;
        _members = members;
        _resourceAccess = resourceAccess;
        _time = time;
        _profiles = profiles;
        _rows = rows;
    }

    public async Task<ServiceResult<GroupDuesDto>> Handle(GetGroupDuesQuery request, CancellationToken cancellationToken)
    {
        var (group, failure) = await _access.OpenAsync<GroupDuesDto>(request.GroupKey, cancellationToken);
        if (group is null)
        {
            return failure!;
        }

        var kurinKey = group.KurinKey;
        await _accrual.AccrueKurinAsync(kurinKey, cancellationToken);

        var kurinRates = await _dues.KurinDuesRates.GetForKurinAsync(kurinKey, cancellationToken);
        var groupRates = await _dues.GroupDuesRates.GetForKurinAsync(kurinKey, cancellationToken);
        var concessions = await _dues.DuesConcessions.GetForKurinAsync(kurinKey, cancellationToken);
        var charges = await _dues.DuesCharges.GetForKurinAsync(kurinKey, cancellationToken);
        var entries = await _dues.DuesEntries.GetForKurinAsync(kurinKey, cancellationToken);
        var grants = await _dues.DuesConcessionGrants.GetForKurinAsync(kurinKey, cancellationToken);
        var names = await DuesNames.LoadAsync(_memberships, _members, kurinKey, cancellationToken);

        var ledger = new DuesLedger(kurinRates, groupRates, concessions, charges, entries, grants);
        var accounts = ledger.Accounts();
        var krai = await _profiles.DuesKraiForKurinAsync(kurinKey, cancellationToken);
        var current = krai.QuarterOf(_time.GetUtcNow());

        var tags = await _rows.TagsAsync(kurinKey, cancellationToken);
        var accountDtos = DuesAccountRows.Map(accounts, group.GroupKey, ledger, names, grants, current, tags);

        var entryDtos = entries
            .Where(e => e.GroupKey == group.GroupKey)
            .OrderByDescending(e => e.OccurredOn)
            .ThenByDescending(e => e.CreatedDate)
            .Select(names.ToDto)
            .ToList();

        var box = ledger.GroupBox(group.GroupKey, accounts);
        var handover = ledger.KurinBox(accounts).Handovers.FirstOrDefault(h => h.GroupKey == group.GroupKey)
            ?? new GroupHandover(group.GroupKey, 0, 0, 0);

        var canKeep = await _resourceAccess.CheckAccessAsync(ResourceType.GroupDues, ResourceAction.Create, group.GroupKey, cancellationToken);
        var canVerify = await _resourceAccess.CheckAccessAsync(ResourceType.GroupDues, ResourceAction.Manage, group.GroupKey, cancellationToken);
        var canSetKurinRates = await _resourceAccess.CheckAccessAsync(ResourceType.KurinDues, ResourceAction.Update, kurinKey, cancellationToken);

        return new ServiceResult<GroupDuesDto>(ResultType.Success, new GroupDuesDto
        {
            GroupKey = group.GroupKey,
            KurinKey = kurinKey,
            GroupName = group.Name,
            Currency = krai.Currency,
            NumberLocale = krai.NumberLocale,
            Upstream = krai.Dues.Upstream,
            KraiConcessions = krai.Dues.Concessions.Select(r => r.ToDto()).ToList(),
            CurrentQuarter = current.ToDto(),
            Years = Years(charges, current),
            KurinRates = kurinRates.Select(r => r.ToDto()).ToList(),
            GroupRates = groupRates.Where(r => r.GroupKey == group.GroupKey).Select(r => new GroupDuesRateDto
            {
                FromQuarter = DuesQuarter.FromIndex(r.FromQuarter).ToDto(),
                GroupShare = r.GroupShare
            }).ToList(),
            Accounts = accountDtos,
            Tags = tags.All,
            Entries = entryDtos,
            Box = new DuesBoxDto
            {
                Cash = box.Box.Cash,
                Card = box.Box.Card,
                Total = box.Box.Total,
                ToForward = box.Box.ToForward,
                // Never reported for a гурток's box; only the kurin's shows it.
                Ahead = 0,
                Own = box.Box.Own,
                InTransit = box.InTransit
            },
            Handover = new DuesHandoverDto
            {
                OwedUp = handover.OwedUp,
                Transferred = handover.Transferred,
                Received = handover.Received,
                Outstanding = handover.Outstanding
            },
            People = names.People,
            Viewer = new DuesViewerDto { CanKeep = canKeep.IsAllowed, CanVerify = canVerify.IsAllowed, CanSetKurinRates = canSetKurinRates.IsAllowed }
        });
    }

    /// <summary>Every пластовий рік from the first charge to today, newest first.</summary>
    private static IReadOnlyList<PlastYearDto> Years(IReadOnlyList<DuesCharge> charges, DuesQuarter current) =>
        DuesMapping.Years(charges.Count == 0 ? current : DuesQuarter.FromIndex(charges.Min(c => c.Quarter)), current);
}
