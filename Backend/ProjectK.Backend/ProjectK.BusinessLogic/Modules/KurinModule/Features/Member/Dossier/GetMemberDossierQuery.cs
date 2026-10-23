using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Members;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Dossier;

/// <summary>
/// Opens a person's box. <paramref name="Include"/> names the folders to read; empty means the
/// index alone — what is in there, without any of it.
/// </summary>
public sealed record GetMemberDossierQuery(
    Guid MemberKey,
    IReadOnlyCollection<string> Include) : IRequest<ServiceResult<MemberDossierResponse>>;

/// <summary>
/// Composes the dossier. Everything the member module owns is read once, from the person's own
/// record; the two folders that belong to other modules — where they have belonged, and what they
/// have earned — are asked for through those modules' contracts and never joined to. That is the
/// whole point of the shape: the same composition works when those modules answer over a network.
/// </summary>
public sealed class GetMemberDossierQueryHandler
    : IRequestHandler<GetMemberDossierQuery, ServiceResult<MemberDossierResponse>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IMembershipDirectory _memberships;
    private readonly IMemberProgressDirectory _progress;
    private readonly IMapper _mapper;
    private readonly IMemberFieldAccess _fieldAccess;
    private readonly ICurrentUserContext _currentUser;

    public GetMemberDossierQueryHandler(
        IMemberUnitOfWork unitOfWork,
        IMembershipDirectory memberships,
        IMemberProgressDirectory progress,
        IMapper mapper,
        IMemberFieldAccess fieldAccess,
        ICurrentUserContext currentUser)
    {
        _unitOfWork = unitOfWork;
        _memberships = memberships;
        _progress = progress;
        _mapper = mapper;
        _fieldAccess = fieldAccess;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<MemberDossierResponse>> Handle(
        GetMemberDossierQuery request,
        CancellationToken cancellationToken)
    {
        if (request.MemberKey == Guid.Empty)
        {
            return ServiceResult<MemberDossierResponse>.Failure(
                ResultType.BadRequest,
                "MemberKeyRequired",
                "MemberKey cannot be empty.");
        }

        var unknown = request.Include
            .Where(folder => !MemberDossierFolders.Exists(folder))
            .ToList();

        if (unknown.Count > 0)
        {
            return ServiceResult<MemberDossierResponse>.Failure(
                ResultType.BadRequest,
                "UnknownDossierFolder",
                $"No such folder: {string.Join(", ", unknown)}. Known folders: {string.Join(", ", MemberDossierFolders.All)}.");
        }

        var person = await _unitOfWork.Members.GetByKeyAsync(request.MemberKey, cancellationToken);
        if (person is null)
        {
            return new ServiceResult<MemberDossierResponse>(ResultType.NotFound);
        }

        // The person's own record already carries everything the member module keeps about them,
        // and mapping it reuses the profile's own photo and history mapping rather than repeating it.
        var mapped = _mapper.Map<MemberResponse>(person);

        var wants = new HashSet<string>(request.Include, StringComparer.OrdinalIgnoreCase);

        var memberships = wants.Contains(MemberDossierFolders.Memberships)
            ? await _memberships.GetForMemberAsync(request.MemberKey, cancellationToken)
            : null;

        var mask = await _fieldAccess.ForViewerAsync(cancellationToken);
        var isSelf = mask.IsSelf(person.UserKey);
        // Where they stand is read only when it can change the answer.
        var showsBirthDate = mask.Shows(MemberDataGroup.BirthDate, person.UserKey, null)
            || mask.Shows(MemberDataGroup.BirthDate, person.UserKey, await GroupHereAsync(person.MemberKey, memberships, cancellationToken));

        var progress = wants.Contains(MemberDossierFolders.Progress)
            ? await _progress.GetForMemberAsync(request.MemberKey, cancellationToken)
            : null;

        // A folder that was read is counted from what came back; one that was not is counted by
        // the module that holds it, so the index costs nothing beyond two counts.
        var membershipCount = memberships?.Count
            ?? await _memberships.CountForMemberAsync(request.MemberKey, cancellationToken);
        var progressCount = progress?.Count
            ?? await _progress.CountForMemberAsync(request.MemberKey, cancellationToken);

        return new ServiceResult<MemberDossierResponse>(
            ResultType.Success,
            new MemberDossierResponse
            {
                Profile = new MemberDossierProfile(
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
                Folders =
                [
                    new(MemberDossierFolders.Memberships, membershipCount),
                    new(MemberDossierFolders.Offices, mapped.LeadershipHistories.Count),
                    new(MemberDossierFolders.Levels, mapped.PlastLevelHistories.Count),
                    new(MemberDossierFolders.Awards, mapped.Awards.Count),
                    new(MemberDossierFolders.Warnings, mapped.Warnings.Count),
                    new(MemberDossierFolders.Progress, progressCount)
                ],
                Memberships = memberships,
                Offices = wants.Contains(MemberDossierFolders.Offices)
                    ? [.. mapped.LeadershipHistories]
                    : null,
                Levels = wants.Contains(MemberDossierFolders.Levels)
                    ? [.. mapped.PlastLevelHistories]
                    : null,
                Awards = wants.Contains(MemberDossierFolders.Awards)
                    ? [.. mapped.Awards]
                    : null,
                Warnings = wants.Contains(MemberDossierFolders.Warnings)
                    ? [.. mapped.Warnings]
                    : null,
                Progress = progress
            });
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
