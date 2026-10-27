using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.ProbeEdition;

/// <summary>
/// Every гурток's probe edition, read for the kurin's settings. The youths' standings are progress
/// data, so the route that serves this is held to whoever may change the kurin.
/// </summary>
public sealed record GetKurinProbeEditionsQuery(Guid KurinKey) : IRequest<ServiceResult<KurinProbeEditionsDto>>;

public sealed class GetKurinProbeEditionsQueryHandler : IRequestHandler<GetKurinProbeEditionsQuery, ServiceResult<KurinProbeEditionsDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbesCatalog _catalog;
    private readonly IProbeEditionHolds _holds;
    private readonly IMemberDirectory _members;
    private readonly IResourceAccessService _access;

    public GetKurinProbeEditionsQueryHandler(
        IUnitOfWork unitOfWork,
        IProbesCatalog catalog,
        IProbeEditionHolds holds,
        IMemberDirectory members,
        IResourceAccessService access)
    {
        _unitOfWork = unitOfWork;
        _catalog = catalog;
        _holds = holds;
        _members = members;
        _access = access;
    }

    public async Task<ServiceResult<KurinProbeEditionsDto>> Handle(GetKurinProbeEditionsQuery request, CancellationToken cancellationToken)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return ServiceResult<KurinProbeEditionsDto>.Failure(ResultType.NotFound, "KurinNotFound", "Kurin was not found.");
        }

        var editions = OfferedProbeEditions.OfferedTo(_catalog, kurin.KraiCode);
        var groups = (await _unitOfWork.Groups.GetAllAsync(kurin.KurinKey, cancellationToken))
            .OrderBy(group => group.Name, StringComparer.CurrentCulture)
            .ToList();

        var youthByGroup = (await _unitOfWork.Memberships.GetRecordsInKurinAsync(kurin.KurinKey, cancellationToken))
            .Where(membership => membership.LeftAtUtc is null && membership.Kind == MembershipKind.Youth && membership.GroupKey is not null)
            .GroupBy(membership => membership.GroupKey!.Value)
            .ToDictionary(group => group.Key, group => group.Select(membership => membership.MemberKey).Distinct().ToList());
        var everyone = youthByGroup.Values.SelectMany(keys => keys).Distinct().ToList();

        var holds = await _holds.ForMembersAsync(everyone, cancellationToken);
        var names = everyone.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _members.GetByKeysAsync(everyone, cancellationToken)).ToDictionary(member => member.MemberKey, member => member.FullName);

        var rows = new List<GroupProbeEditionDto>();
        foreach (var group in groups)
        {
            var canChange = (await _access.CheckAccessAsync(
                ResourceType.GroupProbeEdition, ResourceAction.Update, group.GroupKey, cancellationToken)).IsAllowed;
            var members = youthByGroup.GetValueOrDefault(group.GroupKey, [])
                .Select(memberKey => Standing(memberKey, names.GetValueOrDefault(memberKey, string.Empty), holds.GetValueOrDefault(memberKey)))
                .OrderBy(member => member.FullName, StringComparer.CurrentCulture)
                .ToList();

            rows.Add(new GroupProbeEditionDto(group.GroupKey, group.Name, OfferedProbeEditions.Effective(editions, group.ProbeEdition), canChange, members));
        }

        return new ServiceResult<KurinProbeEditionsDto>(ResultType.Success, new KurinProbeEditionsDto(editions, rows));
    }

    private static GroupMemberStandingDto Standing(Guid memberKey, string fullName, ProbeEditionHold? hold) =>
        hold is null
            ? new GroupMemberStandingDto(memberKey, fullName, HasStarted: false, Edition: null, ProbeTitle: null, IsHeld: false)
            : new GroupMemberStandingDto(memberKey, fullName, HasStarted: true, hold.Edition, hold.ProbeTitle, hold.IsHeld);
}
