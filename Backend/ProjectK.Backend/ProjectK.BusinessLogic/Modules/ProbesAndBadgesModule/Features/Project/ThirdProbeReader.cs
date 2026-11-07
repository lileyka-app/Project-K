using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Project;

/// <summary>
/// Builds the «Третя проба» tab for the viewer. Every write answers with it too, so the clients
/// keep one picture of the probe and never stitch partial answers together.
/// </summary>
public interface IThirdProbeReader
{
    Task<ThirdProbeResponse> ReadAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default);
}

public sealed class ThirdProbeReader : IThirdProbeReader
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbeProjectAccess _access;
    private readonly IMemberProbeLine _line;
    private readonly IBadgesCatalogService _badges;
    private readonly IBlobReadLinks _links;

    public ThirdProbeReader(
        IUnitOfWork unitOfWork,
        IProbeProjectAccess access,
        IMemberProbeLine line,
        IBadgesCatalogService badges,
        IBlobReadLinks links)
    {
        _unitOfWork = unitOfWork;
        _access = access;
        _line = line;
        _badges = badges;
        _links = links;
    }

    public async Task<ThirdProbeResponse> ReadAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default)
    {
        var line = await _line.ForMemberAsync(memberKey, cancellationToken);
        var place = line.ToList().FindIndex(probe => probe.Order == ProbeProjectRules.ProbeOrder && probe.Id == probeId);
        if (place < 0)
        {
            return new ThirdProbeResponse(false, false, false, false, null, [], [], ProbeProjectMapping.Limits);
        }

        var file = await _unitOfWork.ProbeProjects.GetFileAsync(memberKey, probeId, cancellationToken);
        var projects = await _unitOfWork.ProbeProjects.GetForMemberProbeAsync(memberKey, probeId, cancellationToken);
        var previousClosedOn = place > 0 ? await ClosedOnAsync(memberKey, line[place - 1].Id, cancellationToken) : null;
        var openedOn = file?.OpenedOn ?? previousClosedOn;
        var stored = await _unitOfWork.ProbeProjects.GetStoredBytesAsync(memberKey, probeId, cancellationToken);

        var canSign = await _access.CanSignAsync(memberKey, cancellationToken);
        var canEdit = canSign || await _access.CanEditAsync(memberKey, cancellationToken);
        var canUnsign = await _access.CanUnsignAsync(memberKey, cancellationToken);

        return new ThirdProbeResponse(
            true,
            canEdit,
            canSign,
            canUnsign,
            ToResponse(file, openedOn, file?.OpenedOn is null && openedOn is not null, stored),
            projects
                .OrderBy(project => project.IsSpare)
                .ThenBy(project => project.CreatedDate)
                .Select(project => ProbeProjectMapping.ToResponse(project, _links))
                .ToList(),
            await BadgesAsync(memberKey, openedOn, cancellationToken),
            ProbeProjectMapping.Limits);
    }

    private async Task<DateOnly?> ClosedOnAsync(Guid memberKey, string probeId, CancellationToken cancellationToken)
    {
        var progress = await _unitOfWork.ProbeProgresses.GetByMemberAndProbeIdAsync(memberKey, probeId, cancellationToken);
        var closedAt = progress?.CompletedAtUtc ?? progress?.VerifiedAtUtc;
        return closedAt is { } at ? DateOnly.FromDateTime(at) : null;
    }

    private async Task<IReadOnlyList<ThirdProbeBadgeResponse>> BadgesAsync(Guid memberKey, DateOnly? openedOn, CancellationToken cancellationToken)
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
                return new ThirdProbeBadgeResponse(
                    progress.BadgeId,
                    badge?.Title is { Length: > 0 } title ? title : progress.BadgeId,
                    badge?.ImagePath,
                    badge?.Specialization,
                    progress.ReviewedAtUtc,
                    during);
            })
            .ToList();
    }

    private ThirdProbeFileResponse ToResponse(ThirdProbeFile? file, DateOnly? openedOn, bool derived, long stored) => new(
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
            ? new ThirdProbeCertificateResponse(_links.For(blob)!, file.CampCertificateContentType ?? "image/jpeg", file.CampCertificateFileName)
            : null,
        file?.PhysicalBadgeId,
        file?.Resume ?? string.Empty,
        Stages(file),
        stored,
        file?.UpdatedDate,
        file?.UpdatedByName);

    internal static ThirdProbeStagesResponse Stages(ThirdProbeFile? file) => new(
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
