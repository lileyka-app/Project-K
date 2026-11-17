using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Dtos.ScoreModule;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.ScoreModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Member;

/// <summary>
/// The person's own points this пластовий рік in every kurin where they are a youth in a гурток, with
/// the гурток's place. Read from the same book as the table, so the two never disagree.
/// </summary>
public sealed record GetMyScoreQuery : IRequest<ServiceResult<IReadOnlyList<MyScoreDto>>>;

public sealed class GetMyScoreQueryHandler : IRequestHandler<GetMyScoreQuery, ServiceResult<IReadOnlyList<MyScoreDto>>>
{
    private readonly ICurrentMember _me;
    private readonly ScoreBookReader _books;
    private readonly ICurrentUserContext _currentUser;
    private readonly IKurinDirectory _kurins;

    public GetMyScoreQueryHandler(ICurrentMember me, ScoreBookReader books, ICurrentUserContext currentUser, IKurinDirectory kurins)
    {
        _kurins = kurins;
        _me = me;
        _books = books;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<MyScoreDto>>> Handle(GetMyScoreQuery request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadPersonAsync(cancellationToken);
        if (me is null)
        {
            return ScoreErrors.NoMember<IReadOnlyList<MyScoreDto>>();
        }

        var rows = new List<MyScoreDto>();
        foreach (var membership in me.Memberships.Where(m => m.Kind == MembershipKind.Youth && m.GroupKey.HasValue && me.HasYouthProgramIn(m)))
        {
            // Switched off, точкування keeps its records but shows nowhere.
            if (!await _kurins.IsScoreEnabledAsync(membership.KurinKey, cancellationToken))
            {
                continue;
            }

            var book = await _books.OpenAsync(membership.KurinKey, cancellationToken);
            if (book.ResolvePeriod(new ScorePeriodQuery()) is not { } resolved)
            {
                continue;
            }

            var groupKey = membership.GroupKey!.Value;
            var table = book.Ledger.Groups(resolved.Period, book.GroupNames.Keys);
            var place = table.ToList().FindIndex(t => t.GroupKey == groupKey);
            var mine = book.Ledger.People(resolved.Period).FirstOrDefault(p => p.MembershipKey == membership.MembershipKey);

            rows.Add(new MyScoreDto
            {
                Kurin = MyKurinRefDto.Of(membership, _currentUser.KurinKey),
                GroupKey = groupKey,
                GroupName = book.GroupName(groupKey),
                PeriodLabel = resolved.Dto.Label,
                Total = mine?.Total ?? 0,
                BySource = mine?.BySource ?? new Dictionary<ScoreSource, int>(),
                GroupPlace = place + 1,
                GroupCount = table.Count,
                GroupScore = place >= 0 ? table[place].Score : 0,
                Algorithm = book.Algorithm
            });
        }

        return new ServiceResult<IReadOnlyList<MyScoreDto>>(ResultType.Success, rows);
    }
}
