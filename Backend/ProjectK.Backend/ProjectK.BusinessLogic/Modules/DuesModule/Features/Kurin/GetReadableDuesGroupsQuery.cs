using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Models;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin;

/// <summary>
/// The гуртки whose box the caller may open — what the sidebar links to. A Виховник gets their own,
/// the курінний скарбник and the Звʼязковий every one, a youth none: the box is not theirs to read.
/// </summary>
public sealed record GetReadableDuesGroupsQuery(Guid KurinKey) : IRequest<ServiceResult<IReadOnlyList<DuesGroupLinkDto>>>;

public sealed class GetReadableDuesGroupsQueryHandler : IRequestHandler<GetReadableDuesGroupsQuery, ServiceResult<IReadOnlyList<DuesGroupLinkDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAccessService _resourceAccess;
    private readonly ICurrentUserContext _currentUser;

    public GetReadableDuesGroupsQueryHandler(IUnitOfWork unitOfWork, IResourceAccessService resourceAccess, ICurrentUserContext currentUser)
    {
        _unitOfWork = unitOfWork;
        _resourceAccess = resourceAccess;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<DuesGroupLinkDto>>> Handle(GetReadableDuesGroupsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != request.KurinKey)
        {
            return new ServiceResult<IReadOnlyList<DuesGroupLinkDto>>(ResultType.Forbidden);
        }

        var links = new List<DuesGroupLinkDto>();
        foreach (var group in (await _unitOfWork.Groups.GetAllAsync(request.KurinKey, cancellationToken)).OrderBy(g => g.Name))
        {
            var decision = await _resourceAccess.CheckAccessAsync(ResourceType.GroupDues, ResourceAction.Read, group.GroupKey, cancellationToken);
            if (decision.IsAllowed)
            {
                links.Add(new DuesGroupLinkDto { GroupKey = group.GroupKey, GroupName = group.Name });
            }
        }

        return new ServiceResult<IReadOnlyList<DuesGroupLinkDto>>(ResultType.Success, links);
    }
}
