using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ScoreModule;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.ScoreModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Group;

/// <summary>
/// One гурток's page: its youths' points by source for a period, with those who have since moved on
/// but earned something while here, and everything given by hand.
/// </summary>
public sealed record GetGroupScoreQuery(Guid GroupKey, ScorePeriodQuery Period) : IRequest<ServiceResult<GroupScoreDto>>;

public sealed class GetGroupScoreQueryHandler : IRequestHandler<GetGroupScoreQuery, ServiceResult<GroupScoreDto>>
{
    private readonly ScoreAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IUnitOfWork _unitOfWork;

    public GetGroupScoreQueryHandler(ScoreAccess access, ScoreBookReader books, IUnitOfWork unitOfWork)
    {
        _access = access;
        _books = books;
        _unitOfWork = unitOfWork;
    }

    private static string StandingOf(KurinMembershipRecord membership, Guid groupKey)
    {
        if (membership.LeftAtUtc is not null)
        {
            return "Left";
        }

        return membership.GroupKey == groupKey ? "Current" : "Moved";
    }

    public async Task<ServiceResult<GroupScoreDto>> Handle(GetGroupScoreQuery request, CancellationToken cancellationToken)
    {
        var group = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey, cancellationToken);
        if (group is null)
        {
            return ScoreErrors.NotFound<GroupScoreDto>("There is no such гурток.");
        }

        if (_access.Refuse<GroupScoreDto>(group.KurinKey) is { } refused)
        {
            return refused;
        }

        var book = await _books.OpenAsync(group.KurinKey, cancellationToken);
        if (book.ResolvePeriod(request.Period) is not { } resolved)
        {
            return ScoreErrors.NotFound<GroupScoreDto>("There is no such stage.");
        }

        var table = book.Ledger.Groups(resolved.Period, book.GroupNames.Keys);
        var place = table.Select((t, i) => (t.GroupKey, i)).First(x => x.GroupKey == group.GroupKey).i + 1;
        var canScore = await _access.MayScoreAsync(group.KurinKey, group.GroupKey, ResourceAction.Create, cancellationToken);

        var scored = book.Ledger.People(resolved.Period, group.GroupKey).ToDictionary(p => p.MembershipKey);
        var listed = book.CurrentYouths()
            .Where(y => y.Membership.GroupKey == group.GroupKey)
            .Select(y => y.Membership.MembershipKey)
            .Concat(scored.Keys)
            .Distinct();

        var people = listed
            .Select(key =>
            {
                var membership = book.Memberships[key];
                var total = scored.GetValueOrDefault(key);
                return new ScorePersonRowDto
                {
                    MembershipKey = key,
                    MemberKey = membership.MemberKey,
                    FullName = book.NameOfMembership(key),
                    Standing = StandingOf(membership, group.GroupKey),
                    Total = total?.Total ?? 0,
                    BySource = total?.BySource ?? new Dictionary<ScoreSource, int>()
                };
            })
            .OrderByDescending(p => p.Total)
            .ThenBy(p => p.FullName)
            .ToList();

        var entries = book.Entries
            .Where(e => resolved.Period.Contains(e.OccurredOn))
            .Where(e => e.GroupKey == group.GroupKey
                || (e.MembershipKey is { } mk && book.Ledger.GroupOn(mk, e.OccurredOn) == group.GroupKey))
            .OrderByDescending(e => e.OccurredOn)
            .ThenByDescending(e => e.CreatedDate)
            .Select(e => e.ToDto(book))
            .ToList();

        return new ServiceResult<GroupScoreDto>(ResultType.Success, new GroupScoreDto
        {
            GroupKey = group.GroupKey,
            KurinKey = group.KurinKey,
            GroupName = group.Name,
            Algorithm = book.Algorithm,
            Period = resolved.Dto,
            Periods = book.Periods(),
            Standing = table[place - 1].ToDto(place, group.Name, true),
            GroupCount = table.Count,
            People = people,
            Entries = entries,
            Items = book.Items.Where(i => !i.IsArchived).Select(i => i.ToDto()).ToList(),
            CanScore = canScore
        });
    }
}
