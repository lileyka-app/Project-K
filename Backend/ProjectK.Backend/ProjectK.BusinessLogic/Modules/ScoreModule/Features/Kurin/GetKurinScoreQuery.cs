using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Dtos.ScoreModule;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.ScoreModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.ScoreModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Kurin;

/// <summary>
/// The kurin's table for a period — what the whole kurin sees: the гуртки, or, in a senior kurin that
/// has none, its people.
/// </summary>
public sealed record GetKurinScoreQuery(Guid KurinKey, ScorePeriodQuery Period) : IRequest<ServiceResult<KurinScoreDto>>;

public sealed class GetKurinScoreQueryHandler : IRequestHandler<GetKurinScoreQuery, ServiceResult<KurinScoreDto>>
{
    private readonly ScoreAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IResourceAccessService _resourceAccess;
    private readonly IKurinDirectory _kurins;

    public GetKurinScoreQueryHandler(ScoreAccess access, ScoreBookReader books, IResourceAccessService resourceAccess, IKurinDirectory kurins)
    {
        _access = access;
        _books = books;
        _resourceAccess = resourceAccess;
        _kurins = kurins;
    }

    public async Task<ServiceResult<KurinScoreDto>> Handle(GetKurinScoreQuery request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<KurinScoreDto>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var book = await _books.OpenAsync(request.KurinKey, cancellationToken);
        if (book.ResolvePeriod(request.Period) is not { } resolved)
        {
            return ScoreErrors.NotFound<KurinScoreDto>("There is no such stage.");
        }

        var rows = new List<ScoreGroupRowDto>();
        var canScore = false;
        foreach (var (total, index) in book.Ledger.Groups(resolved.Period, book.GroupNames.Keys).Select((t, i) => (t, i)))
        {
            var canOpen = (await _resourceAccess.CheckAccessAsync(ResourceType.GroupScore, ResourceAction.Read, total.GroupKey, cancellationToken)).IsAllowed;
            canScore |= await _access.MayScoreAsync(request.KurinKey, total.GroupKey, ResourceAction.Create, cancellationToken);
            rows.Add(total.ToDto(index + 1, book.GroupName(total.GroupKey), canOpen));
        }

        canScore |= await _access.MayScoreAsync(request.KurinKey, null, ResourceAction.Create, cancellationToken);

        var hasGroups = await _kurins.HasGroupsAsync(request.KurinKey, cancellationToken);

        return new ServiceResult<KurinScoreDto>(ResultType.Success, new KurinScoreDto
        {
            KurinKey = request.KurinKey,
            Algorithm = book.Algorithm,
            Period = resolved.Dto,
            Periods = book.Periods(),
            Groups = rows,
            HasGroups = hasGroups,
            People = hasGroups ? [] : People(book, resolved.Period),
            Viewer = new KurinScoreViewerDto
            {
                CanScore = canScore,
                CanManage = await _access.MayManageAsync(request.KurinKey, cancellationToken)
            }
        });
    }

    /// <summary>Everyone in the kurin now, and anyone who has left but earned something in the period.</summary>
    private static List<ScorePersonRowDto> People(ScoreBook book, ScorePeriod period)
    {
        var scored = book.Ledger.People(period).ToDictionary(p => p.MembershipKey);
        return book.CurrentYouths()
            .Select(y => y.Membership.MembershipKey)
            .Concat(scored.Keys)
            .Distinct()
            .Where(book.Memberships.ContainsKey)
            .Select(key =>
            {
                var membership = book.Memberships[key];
                var total = scored.GetValueOrDefault(key);
                return new ScorePersonRowDto
                {
                    MembershipKey = key,
                    MemberKey = membership.MemberKey,
                    FullName = book.NameOfMembership(key),
                    Standing = membership.LeftAtUtc is null ? "Current" : "Left",
                    Total = total?.Total ?? 0,
                    BySource = total?.BySource ?? new Dictionary<ScoreSource, int>()
                };
            })
            .OrderByDescending(p => p.Total)
            .ThenBy(p => p.FullName)
            .ToList();
    }
}
