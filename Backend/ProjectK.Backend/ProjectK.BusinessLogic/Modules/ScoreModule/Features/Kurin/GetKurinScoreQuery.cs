using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.ScoreModule;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Kurin;

/// <summary>The table of гуртки for a period — what the whole kurin sees.</summary>
public sealed record GetKurinScoreQuery(Guid KurinKey, ScorePeriodQuery Period) : IRequest<ServiceResult<KurinScoreDto>>;

public sealed class GetKurinScoreQueryHandler : IRequestHandler<GetKurinScoreQuery, ServiceResult<KurinScoreDto>>
{
    private readonly ScoreAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IResourceAccessService _resourceAccess;

    public GetKurinScoreQueryHandler(ScoreAccess access, ScoreBookReader books, IResourceAccessService resourceAccess)
    {
        _access = access;
        _books = books;
        _resourceAccess = resourceAccess;
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

        return new ServiceResult<KurinScoreDto>(ResultType.Success, new KurinScoreDto
        {
            KurinKey = request.KurinKey,
            Algorithm = book.Algorithm,
            Period = resolved.Dto,
            Periods = book.Periods(),
            Groups = rows,
            Viewer = new KurinScoreViewerDto
            {
                CanScore = canScore,
                CanManage = await _access.MayManageAsync(request.KurinKey, cancellationToken)
            }
        });
    }
}
