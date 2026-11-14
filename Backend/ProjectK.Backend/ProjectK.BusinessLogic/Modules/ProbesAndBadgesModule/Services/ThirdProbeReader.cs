using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.ThirdProbe;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Rules.AuthModule;
using ProjectK.Common.Rules.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>
/// Builds the «Третя проба» tab for the viewer. Every write answers with it too, so the clients
/// keep one picture of the probe and never stitch partial answers together.
/// </summary>
public interface IThirdProbeReader
{
    Task<ThirdProbeDto> ReadAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default);
}

public sealed class ThirdProbeReader : IThirdProbeReader
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbeProjectAccess _access;
    private readonly IMemberProbeLine _line;
    private readonly IBadgesCatalogService _badges;
    private readonly IBlobReadLinks _links;
    private readonly IMemberDirectory _members;

    public ThirdProbeReader(
        IUnitOfWork unitOfWork,
        IProbeProjectAccess access,
        IMemberProbeLine line,
        IBadgesCatalogService badges,
        IBlobReadLinks links,
        IMemberDirectory members)
    {
        _members = members;
        _unitOfWork = unitOfWork;
        _access = access;
        _line = line;
        _badges = badges;
        _links = links;
    }

    public async Task<ThirdProbeDto> ReadAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default)
    {
        var line = await _line.ForMemberAsync(memberKey, cancellationToken);
        var place = line.ToList().FindIndex(probe => probe.Order == ProbeProjectRules.ProbeOrder && probe.Id == probeId);
        if (place < 0)
        {
            return new ThirdProbeDto(false, false, false, false, null, [], [], ThirdProbeMapping.Limits);
        }

        var file = await _unitOfWork.ProbeProjects.GetFileAsync(memberKey, probeId, cancellationToken);
        var projects = await _unitOfWork.ProbeProjects.GetForMemberProbeAsync(memberKey, probeId, cancellationToken);
        var previousClosedOn = place > 0 ? await ClosedOnAsync(memberKey, line[place - 1].Id, cancellationToken) : null;
        var openedOn = file?.OpenedOn ?? previousClosedOn;
        var stored = await _unitOfWork.ProbeProjects.GetStoredBytesAsync(memberKey, probeId, cancellationToken);

        var canSign = await _access.CanSignAsync(memberKey, cancellationToken);
        var canEdit = canSign || await _access.CanEditAsync(memberKey, cancellationToken);
        var canUnsign = await _access.CanUnsignAsync(memberKey, cancellationToken);

        return new ThirdProbeDto(
            true,
            canEdit,
            canSign,
            canUnsign,
            ToResponse(file, openedOn, file?.OpenedOn is null && openedOn is not null, stored),
            projects
                .OrderBy(project => project.IsSpare)
                .ThenBy(project => project.CreatedDate)
                .Select(project => ThirdProbeMapping.ToDto(project, _links))
                .ToList(),
            await BadgesAsync(memberKey, openedOn, cancellationToken),
            ThirdProbeMapping.Limits,
            canEdit ? await MentorOptionsAsync(memberKey, cancellationToken) : null);
    }

    private async Task<ThirdProbeMentorOptionsDto> MentorOptionsAsync(Guid memberKey, CancellationToken cancellationToken)
    {
        var zvyazkovyi = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi);
        var zvyazkovi = new List<string>();
        var ownGroup = new List<string>();
        var kurinWide = new List<string>();
        foreach (var membership in await _unitOfWork.Memberships.GetActiveForMemberAsync(memberKey, cancellationToken))
        {
            zvyazkovi.AddRange((await _unitOfWork.Leaderships.GetOfficeMembersLookupAsync(membership.KurinKey, LeadershipType.KV, cancellationToken))
                .Where(holder => holder.UserRole == zvyazkovyi && holder.MemberKey != memberKey)
                .Select(holder => $"{holder.FirstName} {holder.LastName}".Trim()));

            foreach (var assignment in (await _unitOfWork.MentorAssignments.GetByKurinKeyAsync(membership.KurinKey, cancellationToken))
                         .Where(assignment => assignment.RevokedAtUtc is null))
            {
                var mentor = await _members.FindByAccountAsync(assignment.MentorUserKey, cancellationToken);
                if (mentor is null || mentor.MemberKey == memberKey)
                {
                    continue;
                }

                (assignment.GroupKey == membership.GroupKey ? ownGroup : kurinWide).Add(mentor.FullName);
            }
        }

        static IReadOnlyList<string> Names(IEnumerable<string> names) =>
            names.Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).ToList();

        return new ThirdProbeMentorOptionsDto(Names(zvyazkovi), Names(ownGroup.Concat(kurinWide.OrderBy(name => name, StringComparer.Ordinal))));
    }

    private async Task<DateOnly?> ClosedOnAsync(Guid memberKey, string probeId, CancellationToken cancellationToken)
    {
        var progress = await _unitOfWork.ProbeProgresses.GetByMemberAndProbeIdAsync(memberKey, probeId, cancellationToken);
        var closedAt = progress?.CompletedAtUtc ?? progress?.VerifiedAtUtc;
        return closedAt is { } at ? DateOnly.FromDateTime(at) : null;
    }

    private async Task<IReadOnlyList<ThirdProbeBadgeDto>> BadgesAsync(Guid memberKey, DateOnly? openedOn, CancellationToken cancellationToken)
    {
        var held = await _unitOfWork.BadgeProgresses.GetByMemberKeyAsync(memberKey, cancellationToken);
        return held
            .Where(progress => progress.Status == BadgeProgressStatus.Confirmed)
            .OrderByDescending(progress => progress.ReviewedAtUtc)
            .Select(progress =>
            {
                var badge = _badges.GetBadgeById(progress.BadgeId);
                var during = openedOn is { } opened
                             && progress.ReviewedAtUtc is { } at
                             && DateOnly.FromDateTime(at) >= opened;
                return new ThirdProbeBadgeDto(
                    progress.BadgeId,
                    badge?.Title is { Length: > 0 } title ? title : progress.BadgeId,
                    badge?.ImagePath,
                    badge?.Specialization,
                    progress.ReviewedAtUtc,
                    during);
            })
            .ToList();
    }

    private ThirdProbeFileDto ToResponse(ThirdProbeFile? file, DateOnly? openedOn, bool derived, long stored) => new(
        openedOn,
        derived,
        file?.MentorZvyazkovyi,
        file?.MentorVporiadnyk,
        file?.MentorReferent,
        file?.ReflectionOn,
        file?.ReflectionWith,
        file?.ReflectionConfirmedAtUtc,
        file?.ReflectionConfirmedByName,
        file?.CampKind,
        file?.CampName,
        file?.CampFrom,
        file?.CampTo,
        file?.CampCertificateBlobName is { } blob
            ? new ThirdProbeCertificateDto(_links.For(blob)!, file.CampCertificateContentType ?? "image/jpeg", file.CampCertificateFileName)
            : null,
        file?.PhysicalBadgeId,
        file?.Resume ?? string.Empty,
        Stages(file),
        stored,
        file?.UpdatedDate,
        file?.UpdatedByName);

    internal static ThirdProbeStagesDto Stages(ThirdProbeFile? file) => new(
        file?.MotivationLetterOn,
        file?.SelfReflectionOn,
        file?.ApplicationSubmittedOn,
        file?.OpenedOn,
        file?.CompletedOn,
        file?.NamedOn,
        file?.ArchivedOn,
        file?.StagesUpdatedAtUtc,
        file?.StagesUpdatedByName);
}
