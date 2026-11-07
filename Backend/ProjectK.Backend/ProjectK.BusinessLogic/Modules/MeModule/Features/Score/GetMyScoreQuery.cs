using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Score;

/// <summary>
/// The person's own points this пластовий рік in every kurin where they are a youth in a гурток, with
/// the гурток's place. Read from the same book as the table, so the two never disagree.
/// </summary>
public sealed record GetMyScoreQuery : IRequest<ServiceResult<IReadOnlyList<MyScoreDto>>>;

public sealed class GetMyScoreQueryHandler : IRequestHandler<GetMyScoreQuery, ServiceResult<IReadOnlyList<MyScoreDto>>>
{
    private readonly MePerson _me;
    private readonly ScoreBookReader _books;
    private readonly ICurrentUserContext _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public GetMyScoreQueryHandler(MePerson me, ScoreBookReader books, ICurrentUserContext currentUser, IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _me = me;
        _books = books;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<MyScoreDto>>> Handle(GetMyScoreQuery request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadAsync(cancellationToken);
        if (me is null)
        {
            return new ServiceResult<IReadOnlyList<MyScoreDto>>(ResultType.Forbidden);
        }

        var rows = new List<MyScoreDto>();
        foreach (var membership in me.Memberships.Where(m => m.Kind == MembershipKind.Youth && m.GroupKey.HasValue && me.HasYouthProgramIn(m)))
        {
            // Switched off, точкування keeps its records but shows nowhere.
            if (!((await _unitOfWork.Kurins.GetByKeyAsync(membership.KurinKey, cancellationToken))?.IsScoreEnabled ?? true))
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
                Kurin = MeKurins.Ref(membership, _currentUser.KurinKey),
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
