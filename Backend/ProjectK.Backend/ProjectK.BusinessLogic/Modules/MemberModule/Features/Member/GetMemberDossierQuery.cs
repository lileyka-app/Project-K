using MediatR;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.MemberModule;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;

/// <summary>
/// Opens a person's box. <paramref name="Include"/> names the folders to read; empty means the
/// index alone — what is in there, without any of it.
/// </summary>
public sealed record GetMemberDossierQuery(
    Guid MemberKey,
    IReadOnlyCollection<string> Include) : IRequest<ServiceResult<MemberDossierDto>>;

/// <summary>
/// Composes the dossier. Everything the member module owns is read once, from the person's own
/// record; the two folders that belong to other modules — where they have belonged, and what they
/// have earned — are asked for through those modules' contracts and never joined to. That is the
/// whole point of the shape: the same composition works when those modules answer over a network.
/// </summary>
public sealed class GetMemberDossierQueryHandler
    : IRequestHandler<GetMemberDossierQuery, ServiceResult<MemberDossierDto>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IMembershipDirectory _memberships;
    private readonly IMemberProgressDirectory _progress;
    private readonly IBlobReadLinks _links;
    private readonly IMemberFieldAccess _fieldAccess;
    private readonly ICurrentUserContext _currentUser;
    private readonly IOfficeDirectory _offices;

    public GetMemberDossierQueryHandler(
        IMemberUnitOfWork unitOfWork,
        IMembershipDirectory memberships,
        IMemberProgressDirectory progress,
        IBlobReadLinks links,
        IMemberFieldAccess fieldAccess,
        ICurrentUserContext currentUser,
        IOfficeDirectory offices)
    {
        _offices = offices;
        _unitOfWork = unitOfWork;
        _memberships = memberships;
        _progress = progress;
        _links = links;
        _fieldAccess = fieldAccess;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<MemberDossierDto>> Handle(
        GetMemberDossierQuery request,
        CancellationToken cancellationToken)
    {
        if (request.MemberKey == Guid.Empty)
        {
            return ServiceResult<MemberDossierDto>.Failure(
                ResultType.BadRequest,
                MemberErrors.MemberKeyRequired,
                "MemberKey cannot be empty.");
        }

        var unknown = request.Include
            .Where(folder => !MemberDossierFolderNames.Exists(folder))
            .ToList();

        if (unknown.Count > 0)
        {
            return ServiceResult<MemberDossierDto>.Failure(
                ResultType.BadRequest,
                MemberErrors.UnknownDossierFolder,
                $"No such folder: {string.Join(", ", unknown)}. Known folders: {string.Join(", ", MemberDossierFolderNames.All)}.");
        }

        var person = await _unitOfWork.Members.GetByKeyAsync(request.MemberKey, cancellationToken);
        if (person is null)
        {
            return ServiceResult<MemberDossierDto>.Failure(ResultType.NotFound, MemberErrors.MemberNotFound, "No such member.");
        }

        // The person's own record already carries everything the member module keeps about them,
        // and mapping it reuses the profile's own photo and history mapping rather than repeating it.
        var offices = await _offices.GetHeldByMemberAsync(person.MemberKey, cancellationToken);
        var mapped = person.ToDto(_links, offices);

        var wants = new HashSet<string>(request.Include, StringComparer.OrdinalIgnoreCase);

        var memberships = wants.Contains(MemberDossierFolderNames.Memberships)
            ? await _memberships.GetForMemberAsync(request.MemberKey, cancellationToken)
            : null;

        var mask = await _fieldAccess.ForViewerAsync(cancellationToken);
        var isSelf = mask.IsSelf(person.UserKey);
        // Where they stand is read only when it can change the answer.
        var showsBirthDate = mask.Shows(MemberDataGroup.BirthDate, person.UserKey, null)
            || mask.Shows(MemberDataGroup.BirthDate, person.UserKey, await GroupHereAsync(person.MemberKey, memberships, cancellationToken));

        var progress = wants.Contains(MemberDossierFolderNames.Progress)
            ? await _progress.GetForMemberAsync(request.MemberKey, cancellationToken)
            : null;

        // A folder that was read is counted from what came back; one that was not is counted by
        // the module that holds it, so the index costs nothing beyond two counts.
        var membershipCount = memberships?.Count
            ?? await _memberships.CountForMemberAsync(request.MemberKey, cancellationToken);
        var progressCount = progress?.Count
            ?? await _progress.CountForMemberAsync(request.MemberKey, cancellationToken);

        return new ServiceResult<MemberDossierDto>(
            ResultType.Success,
            new MemberDossierDto(
                Profile: new MemberDossierProfileDto(
                    person.MemberKey,
                    isSelf ? person.PublicId : null,
                    person.UserKey,
                    person.FirstName,
                    person.MiddleName,
                    person.LastName,
                    showsBirthDate ? person.DateOfBirth : null,
                    mapped.ProfilePhotoUrl,
                    mapped.LatestPlastLevel,
                    person.ProfileVerificationStatus,
                    person.ProfileVerifiedAtUtc),
                Folders:
                [
                    new(MemberDossierFolderNames.Memberships, membershipCount),
                    new(MemberDossierFolderNames.Offices, mapped.LeadershipHistories.Count),
                    new(MemberDossierFolderNames.Levels, mapped.PlastLevelHistories.Count),
                    new(MemberDossierFolderNames.Awards, mapped.Awards.Count),
                    new(MemberDossierFolderNames.Warnings, mapped.Warnings.Count),
                    new(MemberDossierFolderNames.Progress, progressCount)
                ],
                Memberships: memberships?.Select(m => m.InLineOf(LevelsOf(person))).ToList(),
                Offices: wants.Contains(MemberDossierFolderNames.Offices)
                    ? [.. mapped.LeadershipHistories]
                    : null,
                Levels: wants.Contains(MemberDossierFolderNames.Levels)
                    ? [.. mapped.PlastLevelHistories]
                    : null,
                Awards: wants.Contains(MemberDossierFolderNames.Awards)
                    ? [.. mapped.Awards]
                    : null,
                Warnings: wants.Contains(MemberDossierFolderNames.Warnings)
                    ? [.. mapped.Warnings]
                    : null,
                Progress: progress));
    }

    /// <summary>Every ступінь on record and the latest one, which an older record may hold alone.</summary>
    private static List<PlastLevel> LevelsOf(ProjectK.Common.Entities.MemberModule.Member person)
    {
        var levels = person.PlastLevelHistory.Select(h => h.PlastLevel).ToList();
        if (person.LatestPlastLevel is { } latest)
        {
            levels.Add(latest);
        }

        return levels;
    }

    /// <summary>The гурток the person is placed in within the caller's kurin, which the mask is measured against.</summary>
    private async Task<Guid?> GroupHereAsync(
        Guid memberKey,
        IReadOnlyCollection<MembershipRecord>? memberships,
        CancellationToken cancellationToken)
    {
        memberships ??= await _memberships.GetForMemberAsync(memberKey, cancellationToken);
        return memberships.FirstOrDefault(m => m.IsCurrent && m.KurinKey == _currentUser.KurinKey)?.GroupKey;
    }
}
