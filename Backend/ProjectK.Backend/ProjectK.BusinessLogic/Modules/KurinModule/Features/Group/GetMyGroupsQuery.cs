using MediatR;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;

/// <summary>
/// The гуртки the person is in or leads, in every kurin they stand in, with each one's сильветка.
/// Led гуртки are read for every office, курінний's too: the dashboard shows where someone stands,
/// not how far their rights reach. Only for someone who holds an office there: the scope reader
/// also counts a member's own гурток as led, a fallback for authorization that would make every
/// youth a впорядник here.
/// </summary>
public sealed record GetMyGroupsQuery : IRequest<ServiceResult<IReadOnlyList<MyGroupDto>>>;

public sealed class GetMyGroupsQueryHandler : IRequestHandler<GetMyGroupsQuery, ServiceResult<IReadOnlyList<MyGroupDto>>>
{
    private readonly ICurrentMember _me;
    private readonly IResourceScopeReader _scopeReader;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;
    private readonly ICurrentUserContext _currentUser;
    private readonly IOfficeDirectory _offices;

    public GetMyGroupsQueryHandler(
        ICurrentMember me,
        IOfficeDirectory offices,
        IUnitOfWork unitOfWork,
        IBlobReadLinks links,
        ICurrentUserContext currentUser,
        IResourceScopeReader scopeReader)
    {
        _me = me;
        _offices = offices;
        _scopeReader = scopeReader;
        _unitOfWork = unitOfWork;
        _links = links;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<MyGroupDto>>> Handle(GetMyGroupsQuery request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadPersonAsync(cancellationToken);
        if (me is null)
        {
            return KurinErrors.NoMember<IReadOnlyList<MyGroupDto>>();
        }

        var rows = new List<MyGroupDto>();
        foreach (var membership in me.Memberships)
        {
            var holdsOffice = (await _offices.GetForAccountInKurinAsync(me.UserKey, membership.KurinKey, cancellationToken)).Count > 0;
            var led = holdsOffice
                ? (await _scopeReader.GetLedGroupKeysAsync(me.UserKey, membership.KurinKey, cancellationToken)).ToHashSet()
                : new HashSet<Guid>();
            if (led.Count == 0 && membership.GroupKey is null)
            {
                continue;
            }

            var kurin = MyKurinRefDto.Of(membership, _currentUser.KurinKey);
            var groups = await _unitOfWork.Groups.GetAllAsync(membership.KurinKey, cancellationToken);
            rows.AddRange(groups
                .Where(g => g.GroupKey == membership.GroupKey || led.Contains(g.GroupKey))
                .OrderBy(g => g.GroupKey == membership.GroupKey ? 0 : 1)
                .ThenBy(g => g.Name)
                .Select(g => new MyGroupDto
                {
                    GroupKey = g.GroupKey,
                    Kurin = kurin,
                    Name = g.Name,
                    SilhouetteUrl = _links.For(g.SilhouetteBlobName),
                    IsOwn = g.GroupKey == membership.GroupKey,
                    IsLed = led.Contains(g.GroupKey)
                }));
        }

        return new ServiceResult<IReadOnlyList<MyGroupDto>>(ResultType.Success, rows);
    }
}
