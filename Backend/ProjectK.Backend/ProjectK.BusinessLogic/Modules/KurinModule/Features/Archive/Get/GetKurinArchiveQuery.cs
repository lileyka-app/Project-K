using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Archive.Get;

/// <summary>
/// The kurin's archive as far as the caller reaches it: the Звʼязковий sees all of it, a гурток's
/// провід the records of the гуртки they lead. Each row says whether the caller may correct it.
/// </summary>
public sealed record GetKurinArchiveQuery(Guid KurinKey) : IRequest<ServiceResult<KurinArchiveDto>>;

public sealed class GetKurinArchiveQueryHandler : IRequestHandler<GetKurinArchiveQuery, ServiceResult<KurinArchiveDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly ArchiveReach _reach;

    public GetKurinArchiveQueryHandler(IUnitOfWork unitOfWork, IMemberDirectory members, ArchiveReach reach)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _reach = reach;
    }

    public async Task<ServiceResult<KurinArchiveDto>> Handle(GetKurinArchiveQuery request, CancellationToken cancellationToken)
    {
        if (await _reach.ResolveAsync(request.KurinKey, ResourceAction.Read, cancellationToken) is not { } reach)
        {
            return ServiceResult<KurinArchiveDto>.Failure(ResultType.Forbidden, ArchiveErrors.OutOfReach, "The archive is not yours to read.");
        }

        var correcting = await _reach.ResolveAsync(request.KurinKey, ResourceAction.Update, cancellationToken);
        var deleting = await _reach.ResolveAsync(request.KurinKey, ResourceAction.Delete, cancellationToken);

        var offices = (await _unitOfWork.Archive.GetOfficesAsync(request.KurinKey, cancellationToken))
            .Where(office => reach.Reaches([office.GroupKey]))
            .ToList();
        var memberships = (await _unitOfWork.Archive.GetFormerMembershipsAsync(request.KurinKey, cancellationToken))
            .Where(membership => reach.Reaches(membership.Stints.Select(stint => (Guid?)stint.GroupKey)))
            .ToList();
        var dissolved = (await _unitOfWork.Archive.GetDissolvedGroupsAsync(request.KurinKey, cancellationToken))
            .Where(group => reach.Reaches([group.GroupKey]))
            .ToList();

        var people = offices.Select(office => office.MemberKey).Concat(memberships.Select(membership => membership.MemberKey)).Distinct().ToList();
        var names = people.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _members.GetByKeysAsync(people, cancellationToken))
                .GroupBy(member => member.MemberKey)
                .ToDictionary(found => found.Key, found => found.First().FullName);

        return new ServiceResult<KurinArchiveDto>(
            ResultType.Success,
            new KurinArchiveDto(
                offices.Select(office => ToDto(office, names, reach, correcting)).ToList(),
                memberships.Select(membership => ToDto(membership, names, reach, correcting)).ToList(),
                dissolved.Select(group => new DissolvedGroupDto(group.GroupKey, group.Name, DateOnly.FromDateTime(group.DissolvedAtUtc))).ToList(),
                deleting?.KurinWide == true));
    }

    private static ArchivedOfficeDto ToDto(ArchivedOffice office, Dictionary<Guid, string> names, ArchiveReachResult reach, ArchiveReachResult? correcting)
    {
        var own = reach.IsOwn(office.MemberKey);
        return new ArchivedOfficeDto(
            office.HistoryKey,
            office.Type,
            office.Role,
            office.MemberKey,
            names.GetValueOrDefault(office.MemberKey, string.Empty),
            office.StartDate,
            office.EndDate,
            office.GroupKey,
            office.GroupName,
            office.GroupDissolved,
            !own && office.EndDate is not null && correcting?.Reaches([office.GroupKey]) == true,
            own);
    }

    private static ArchivedMembershipDto ToDto(ArchivedMembership membership, Dictionary<Guid, string> names, ArchiveReachResult reach, ArchiveReachResult? correcting)
    {
        var own = reach.IsOwn(membership.MemberKey);
        return new ArchivedMembershipDto(
            membership.MembershipKey,
            membership.MemberKey,
            names.GetValueOrDefault(membership.MemberKey, string.Empty),
            membership.Kind,
            DateOnly.FromDateTime(membership.JoinedAtUtc),
            DateOnly.FromDateTime(membership.LeftAtUtc),
            membership.Stints
                .Select(stint => new ArchivedStintDto(
                    stint.GroupKey,
                    stint.GroupName,
                    stint.FromUtc is { } from ? DateOnly.FromDateTime(from) : null,
                    stint.ToUtc is { } to ? DateOnly.FromDateTime(to) : null))
                .ToList(),
            !own && correcting?.Reaches(membership.Stints.Select(stint => (Guid?)stint.GroupKey)) == true,
            own);
    }
}
