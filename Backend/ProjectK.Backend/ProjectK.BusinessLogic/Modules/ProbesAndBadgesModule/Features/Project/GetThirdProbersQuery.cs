using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Project;

/// <summary>
/// The kurin's third-probers for the referent of the third probe and their own tool: everyone whose
/// third probe has a file or whose second probe is closed, with the stages and how far the
/// requirements are. Only people whose probe the caller may sign are listed.
/// </summary>
public sealed record GetThirdProbersQuery(Guid KurinKey) : IRequest<ServiceResult<IReadOnlyList<ThirdProberStatusResponse>>>;

public sealed class GetThirdProbersQueryHandler
    : IRequestHandler<GetThirdProbersQuery, ServiceResult<IReadOnlyList<ThirdProberStatusResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly IMemberProbeLine _line;
    private readonly IProbeProjectAccess _access;

    public GetThirdProbersQueryHandler(IUnitOfWork unitOfWork, IMemberDirectory members, IMemberProbeLine line, IProbeProjectAccess access)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _line = line;
        _access = access;
    }

    public async Task<ServiceResult<IReadOnlyList<ThirdProberStatusResponse>>> Handle(GetThirdProbersQuery request, CancellationToken cancellationToken)
    {
        var people = await _members.GetByKurinAsync(request.KurinKey, cancellationToken);
        var keys = people.Select(person => person.MemberKey).ToList();
        var files = (await _unitOfWork.ProbeProjects.GetFilesForMembersAsync(keys, cancellationToken))
            .GroupBy(file => (file.MemberKey, file.ProbeId))
            .ToDictionary(group => group.Key, group => group.First());
        var projects = await _unitOfWork.ProbeProjects.GetProjectsForMembersAsync(keys, cancellationToken);
        var progress = (await _unitOfWork.ProbeProgresses.GetByMemberKeysAsync(keys, cancellationToken)).ToList();
        var badges = (await _unitOfWork.BadgeProgresses.GetByMemberKeysAsync(keys, cancellationToken))
            .Where(badge => badge.Status == BadgeProgressStatus.Confirmed)
            .ToList();

        var result = new List<ThirdProberStatusResponse>();
        foreach (var person in people.OrderBy(p => p.LastName).ThenBy(p => p.FirstName))
        {
            var line = await _line.ForMemberAsync(person.MemberKey, cancellationToken);
            var place = line.ToList().FindIndex(probe => probe.Order == ProbeProjectRules.ProbeOrder);
            if (place < 0)
            {
                continue;
            }

            var probeId = line[place].Id;
            files.TryGetValue((person.MemberKey, probeId), out var file);
            var previous = place > 0
                ? progress.FirstOrDefault(p => p.MemberKey == person.MemberKey && p.ProbeId == line[place - 1].Id)
                : null;
            var previousClosedAt = previous?.CompletedAtUtc ?? previous?.VerifiedAtUtc;
            if (file is null && previousClosedAt is null)
            {
                continue;
            }

            if (!await _access.CanSignAsync(person.MemberKey, cancellationToken))
            {
                continue;
            }

            var opened = file?.OpenedOn ?? (previousClosedAt is { } at ? DateOnly.FromDateTime(at) : null);
            var own = projects.Where(p => p.MemberKey == person.MemberKey && p.ProbeId == probeId && !p.IsSpare).ToList();
            result.Add(new ThirdProberStatusResponse(
                person.MemberKey,
                person.FirstName,
                person.LastName,
                probeId,
                ThirdProbeReader.Stages(file),
                own.Count,
                own.Count(p => p.CompletedAtUtc is not null),
                badges.Count(badge => badge.MemberKey == person.MemberKey
                                      && opened is { } from && badge.ReviewedAtUtc is { } confirmed && DateOnly.FromDateTime(confirmed) >= from),
                !string.IsNullOrEmpty(file?.CampName),
                !string.IsNullOrWhiteSpace(file?.Resume),
                file?.ReflectionConfirmedAtUtc is not null));
        }

        return new ServiceResult<IReadOnlyList<ThirdProberStatusResponse>>(ResultType.Success, result);
    }
}
