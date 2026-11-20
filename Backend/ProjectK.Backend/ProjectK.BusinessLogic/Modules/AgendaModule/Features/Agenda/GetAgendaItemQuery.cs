using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AgendaModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Features.Agenda;

/// <summary>One task as the viewer sees it — what the dialog re-reads after a part moves.</summary>
public sealed record GetAgendaItemQuery(Guid AgendaItemKey) : IRequest<ServiceResult<AgendaItemDto>>;

public sealed class GetAgendaItemQueryHandler : IRequestHandler<GetAgendaItemQuery, ServiceResult<AgendaItemDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMembershipDirectory _memberships;
    private readonly IOfficeDirectory _offices;
    private readonly IKurinDirectory _kurins;
    private readonly IKurinTagDirectory _tags;
    private readonly IAgendaAccess _access;
    private readonly ICurrentUserContext _currentUser;
    private readonly UserManager<AppUser> _userManager;

    public GetAgendaItemQueryHandler(IUnitOfWork uow, IMembershipDirectory memberships, IOfficeDirectory offices, IKurinDirectory kurins, IKurinTagDirectory tags, IAgendaAccess access, ICurrentUserContext currentUser, UserManager<AppUser> userManager)
    {
        _uow = uow;
        _memberships = memberships;
        _offices = offices;
        _kurins = kurins;
        _tags = tags;
        _access = access;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    public async Task<ServiceResult<AgendaItemDto>> Handle(GetAgendaItemQuery request, CancellationToken cancellationToken)
    {
        var item = await _uow.AgendaItems.GetByKeyWithAssignmentsAsync(request.AgendaItemKey, cancellationToken);
        if (item is null || item.KurinKey != _currentUser.KurinKey)
        {
            return AgendaErrors.NotFound<AgendaItemDto>();
        }

        var viewer = await _access.BuildViewerAsync(item.KurinKey, cancellationToken);
        // Not found rather than forbidden: an item one may not see is not confirmed to exist.
        if (!AgendaAccess.IsVisibleTo(item, viewer))
        {
            return AgendaErrors.NotFound<AgendaItemDto>();
        }

        var lookups = await AgendaNames.LoadAsync(_uow, _memberships, _offices, _kurins, _tags, item.KurinKey, [item], cancellationToken);
        var roster = await AgendaRosterRules.LoadAsync(_offices, _tags, lookups.MemberGroups, [item], cancellationToken);
        var userNames = await AgendaCreatorNames.ResolveAsync(_userManager, lookups.CreatorNames, [item], cancellationToken);

        return new ServiceResult<AgendaItemDto>(ResultType.Success,
            AgendaItemMapping.Create(item, viewer, AgendaNames.KurinLabel, lookups.GroupNames, lookups.MemberNames, userNames, lookups.TargetLabels, lookups.Categories, roster));
    }
}
