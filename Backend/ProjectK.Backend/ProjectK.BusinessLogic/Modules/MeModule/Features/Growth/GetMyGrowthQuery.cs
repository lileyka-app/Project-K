using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Growth;

/// <summary>The person's проба and вмілості, for the dashboard; empty for anyone outside the youth programme.</summary>
public sealed record GetMyGrowthQuery : IRequest<ServiceResult<MyGrowthDto>>;

public sealed class GetMyGrowthQueryHandler : IRequestHandler<GetMyGrowthQuery, ServiceResult<MyGrowthDto>>
{
    private const int NextPointsShown = 3;
    private const int ConfirmedShown = 6;

    private readonly MePerson _me;
    private readonly IMemberProgressDirectory _progress;
    private readonly IProbesCatalogService _probes;
    private readonly IBadgesCatalogService _badges;
    private readonly IMemberProbeLine _line;

    public GetMyGrowthQueryHandler(
        MePerson me,
        IMemberProgressDirectory progress,
        IProbesCatalogService probes,
        IBadgesCatalogService badges,
        IMemberProbeLine line)
    {
        _me = me;
        _progress = progress;
        _probes = probes;
        _badges = badges;
        _line = line;
    }

    public async Task<ServiceResult<MyGrowthDto>> Handle(GetMyGrowthQuery request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadAsync(cancellationToken);
        if (me is null)
        {
            return new ServiceResult<MyGrowthDto>(ResultType.Forbidden);
        }

        if (!me.HasYouthProgram)
        {
            return new ServiceResult<MyGrowthDto>(ResultType.Success, new MyGrowthDto { MemberKey = me.Person.MemberKey });
        }

        var memberKey = me.Person.MemberKey;
        var progress = await _progress.GetForMemberAsync(memberKey, cancellationToken);

        return new ServiceResult<MyGrowthDto>(ResultType.Success, new MyGrowthDto
        {
            MemberKey = memberKey,
            HasYouthProgram = true,
            Probe = await ProbeAsync(memberKey, progress.Probes, cancellationToken),
            Badges = Badges(progress.Badges)
        });
    }

    /// <summary>The проба to show, with how far it has come and what is next in it.</summary>
    private async Task<MyProbeDto?> ProbeAsync(Guid memberKey, IReadOnlyCollection<ProbeProgressRecord> progresses, CancellationToken cancellationToken)
    {
        var byId = progresses.ToDictionary(p => p.ProbeId);
        var line = await _line.ForMemberAsync(memberKey, cancellationToken);
        var currentId = GrowthSummary.CurrentProbeId(line.Select(p => p.Id), byId.Values.Select(p => (p.ProbeId, p.Status)));
        if (currentId is null || _probes.GetGroupedProbeById(currentId) is not { } probe)
        {
            return null;
        }

        var signed = (await _progress.GetSignedPointIdsAsync(memberKey, currentId, cancellationToken)).ToHashSet();

        var points = probe.Sections
            .SelectMany(s => s.Points.Select(p => new MyProbePointDto { PointId = p.Id, SectionCode = s.Code, Title = p.Title }))
            .ToList();

        return new MyProbeDto
        {
            ProbeId = probe.Id,
            Title = probe.Title,
            Status = byId.TryGetValue(currentId, out var progress) ? progress.Status : ProbeProgressStatus.NotStarted,
            SignedPoints = points.Count(p => signed.Contains(p.PointId)),
            TotalPoints = points.Count,
            NextPoints = points.Where(p => !signed.Contains(p.PointId)).Take(NextPointsShown).ToList()
        };
    }

    private MyBadgesDto Badges(IReadOnlyCollection<BadgeProgressRecord> progresses)
    {
        var rows = progresses.Select(p =>
        {
            var badge = _badges.GetBadgeById(p.BadgeId);
            return new MyBadgeDto
            {
                BadgeId = p.BadgeId,
                Title = badge?.Title ?? p.BadgeId,
                ImagePath = badge?.ImagePath,
                Status = p.Status,
                SubmittedAtUtc = p.SubmittedAtUtc,
                ReviewedAtUtc = p.ReviewedAtUtc
            };
        }).ToList();

        var confirmed = rows.Where(r => r.Status == BadgeProgressStatus.Confirmed).OrderByDescending(r => r.ReviewedAtUtc).ToList();
        return new MyBadgesDto
        {
            OnReview = rows.Where(r => r.Status == BadgeProgressStatus.Submitted).OrderByDescending(r => r.SubmittedAtUtc).ToList(),
            InWork = rows.Where(r => r.Status is BadgeProgressStatus.Draft or BadgeProgressStatus.Rejected).OrderBy(r => r.Title).ToList(),
            Confirmed = confirmed.Take(ConfirmedShown).ToList(),
            ConfirmedCount = confirmed.Count
        };
    }
}
