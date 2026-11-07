using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Get;

/// <summary>One task as the viewer sees it — what the dialog re-reads after a part moves.</summary>
public sealed record GetAgendaItemQuery(Guid AgendaItemKey) : IRequest<ServiceResult<AgendaItemResponse>>;

public sealed class GetAgendaItemQueryHandler : IRequestHandler<GetAgendaItemQuery, ServiceResult<AgendaItemResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IAgendaAccess _access;
    private readonly ICurrentUserContext _currentUser;
    private readonly UserManager<AppUser> _userManager;

    public GetAgendaItemQueryHandler(IUnitOfWork uow, IMemberDirectory members, IAgendaAccess access, ICurrentUserContext currentUser, UserManager<AppUser> userManager)
    {
        _uow = uow;
        _members = members;
        _access = access;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    public async Task<ServiceResult<AgendaItemResponse>> Handle(GetAgendaItemQuery request, CancellationToken cancellationToken)
    {
        var item = await _uow.AgendaItems.GetByKeyWithAssignmentsAsync(request.AgendaItemKey, cancellationToken);
        if (item is null || item.KurinKey != _currentUser.KurinKey)
        {
            return ServiceResult<AgendaItemResponse>.Failure(ResultType.NotFound, "AGENDA_NOT_FOUND", "Agenda item was not found.");
        }

        var viewer = await _access.BuildViewerAsync(item.KurinKey, cancellationToken);
        // Not found rather than forbidden: an item one may not see is not confirmed to exist.
        if (!AgendaPermissions.IsVisibleTo(item, viewer))
        {
            return ServiceResult<AgendaItemResponse>.Failure(ResultType.NotFound, "AGENDA_NOT_FOUND", "Agenda item was not found.");
        }

        var lookups = await AgendaLookups.LoadAsync(_uow, _members, item.KurinKey, [item], cancellationToken);
        var roster = await AgendaRoster.LoadAsync(_uow, lookups.MemberGroups, [item], cancellationToken);
        var userNames = await AgendaCreatorNames.ResolveAsync(_userManager, lookups.CreatorNames, [item], cancellationToken);

        return new ServiceResult<AgendaItemResponse>(ResultType.Success,
            AgendaItemResponseFactory.Create(item, viewer, AgendaLookups.KurinLabel, lookups.GroupNames, lookups.MemberNames, userNames, lookups.TargetLabels, lookups.Categories, roster));
    }
}
