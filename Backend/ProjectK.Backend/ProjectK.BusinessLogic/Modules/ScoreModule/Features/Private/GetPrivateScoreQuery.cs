using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Dtos.ScoreModule;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Private;

public sealed record GetPrivateScoreQuery(Guid KurinKey, ScorePeriodQuery Period) : IRequest<ServiceResult<PrivateScoreDto>>;

public sealed class GetPrivateScoreQueryHandler : IRequestHandler<GetPrivateScoreQuery, ServiceResult<PrivateScoreDto>>
{
    private readonly PrivateScoreAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IScoreUnitOfWork _score;

    public GetPrivateScoreQueryHandler(PrivateScoreAccess access, ScoreBookReader books, IScoreUnitOfWork score)
    {
        _access = access;
        _books = books;
        _score = score;
    }

    public async Task<ServiceResult<PrivateScoreDto>> Handle(GetPrivateScoreQuery request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseAsync<PrivateScoreDto>(request.KurinKey, ResourceAction.Read, cancellationToken) is { } refused)
        {
            return refused;
        }

        var book = await _books.OpenAsync(request.KurinKey, cancellationToken);
        if (book.ResolvePeriod(request.Period) is not { } resolved)
        {
            return ScoreErrors.NotFound<PrivateScoreDto>("There is no such stage.");
        }

        var criteria = await _score.PrivateScoreCriteria.GetForKurinAsync(request.KurinKey, cancellationToken);
        var names = criteria.ToDictionary(c => c.PrivateScoreCriterionKey, c => c.Name);
        var entries = (await _score.PrivateScoreEntries.GetForKurinAsync(request.KurinKey, cancellationToken))
            .Where(e => resolved.Period.Contains(e.OccurredOn))
            .ToList();
        var publicTotals = book.Ledger.People(resolved.Period).ToDictionary(p => p.MembershipKey, p => p.Total);
        var byPerson = entries.ToLookup(e => e.MembershipKey);

        var people = book.CurrentYouths()
            .Select(y => new PrivateScorePersonDto
            {
                MembershipKey = y.Membership.MembershipKey,
                MemberKey = y.Membership.MemberKey,
                FullName = y.Person.FullName,
                GroupKey = y.Membership.GroupKey,
                GroupName = book.GroupName(y.Membership.GroupKey),
                PublicTotal = publicTotals.GetValueOrDefault(y.Membership.MembershipKey),
                PrivateTotal = byPerson[y.Membership.MembershipKey].Sum(e => e.Points),
                ByCriterion = byPerson[y.Membership.MembershipKey]
                    .Where(e => e.PrivateScoreCriterionKey.HasValue)
                    .GroupBy(e => e.PrivateScoreCriterionKey!.Value)
                    .ToDictionary(g => g.Key, g => g.Sum(e => e.Points)),
                Uncategorised = byPerson[y.Membership.MembershipKey].Where(e => e.PrivateScoreCriterionKey is null).Sum(e => e.Points)
            })
            .OrderByDescending(p => p.PrivateTotal)
            .ThenBy(p => p.FullName)
            .ToList();

        return new ServiceResult<PrivateScoreDto>(ResultType.Success, new PrivateScoreDto
        {
            KurinKey = request.KurinKey,
            Period = resolved.Dto,
            Periods = book.Periods(),
            Criteria = criteria.Select(c => new PrivateScoreCriterionDto { PrivateScoreCriterionKey = c.PrivateScoreCriterionKey, Name = c.Name, IsArchived = c.IsArchived }).ToList(),
            People = people,
            Entries = entries
                .OrderByDescending(e => e.OccurredOn)
                .ThenByDescending(e => e.CreatedDate)
                .Select(e => new PrivateScoreEntryDto
                {
                    PrivateScoreEntryKey = e.PrivateScoreEntryKey,
                    MembershipKey = e.MembershipKey,
                    MemberKey = book.MemberKeyOf(e.MembershipKey) ?? Guid.Empty,
                    MemberName = book.NameOfMembership(e.MembershipKey),
                    PrivateScoreCriterionKey = e.PrivateScoreCriterionKey,
                    CriterionName = e.PrivateScoreCriterionKey is { } ck ? names.GetValueOrDefault(ck) : null,
                    Points = e.Points,
                    Note = e.Note,
                    OccurredOn = e.OccurredOn,
                    CreatedByName = book.NameOfAccount(e.CreatedByUserKey),
                    CreatedAtUtc = DateTime.SpecifyKind(e.CreatedDate, DateTimeKind.Utc)
                })
                .ToList()
        });
    }
}
