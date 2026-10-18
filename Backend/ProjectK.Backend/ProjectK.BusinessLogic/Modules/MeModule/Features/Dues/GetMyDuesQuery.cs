using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Dues;

/// <summary>
/// The person's вкладка in every kurin that has ever charged them. A kurin with no account for them —
/// no rates, or a membership that is never charged — is not a row: there is nothing to owe there.
/// </summary>
public sealed record GetMyDuesQuery : IRequest<ServiceResult<IReadOnlyList<MyDuesDto>>>;

public sealed class GetMyDuesQueryHandler : IRequestHandler<GetMyDuesQuery, ServiceResult<IReadOnlyList<MyDuesDto>>>
{
    private readonly MePerson _me;
    private readonly DuesLedgerReader _ledgers;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;
    private readonly IKraiProfileProvider _profiles;

    public GetMyDuesQueryHandler(MePerson me, DuesLedgerReader ledgers, ICurrentUserContext currentUser, TimeProvider time, IKraiProfileProvider profiles)
    {
        _me = me;
        _ledgers = ledgers;
        _currentUser = currentUser;
        _time = time;
        _profiles = profiles;
    }

    public async Task<ServiceResult<IReadOnlyList<MyDuesDto>>> Handle(GetMyDuesQuery request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadAsync(cancellationToken);
        if (me is null)
        {
            return new ServiceResult<IReadOnlyList<MyDuesDto>>(ResultType.Forbidden);
        }

        var rows = new List<MyDuesDto>();

        foreach (var membership in me.Memberships)
        {
            var krai = await _profiles.DuesKraiForKurinAsync(membership.KurinKey, cancellationToken);
            var quarter = krai.QuarterOf(_time.GetUtcNow());
            var ledger = await _ledgers.OpenAsync(membership.KurinKey, cancellationToken);
            var accounts = ledger.Accounts().Where(a => a.MembershipKey == membership.MembershipKey).ToList();
            if (accounts.Count == 0)
            {
                continue;
            }

            rows.Add(new MyDuesDto
            {
                Kurin = MeKurins.Ref(membership, _currentUser.KurinKey),
                GroupName = membership.GroupName,
                Currency = krai.Currency,
                NumberLocale = krai.NumberLocale,
                QuarterYear = quarter.Year,
                QuarterNumber = quarter.Number,
                Balance = accounts.Sum(a => a.Balance),
                QuarterRate = membership.GroupKey is { } group ? ledger.AmountFor(membership.MembershipKey, group, quarter).Total : null,
                IsConcession = ledger.IsConcession(membership.MembershipKey, quarter)
            });
        }

        return new ServiceResult<IReadOnlyList<MyDuesDto>>(ResultType.Success, rows);
    }
}
