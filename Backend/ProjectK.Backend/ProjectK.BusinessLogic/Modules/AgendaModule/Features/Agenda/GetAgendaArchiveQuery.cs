using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AgendaModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Features.Agenda;

/// <summary>The archived tasks the viewer may see, newest first, with the kurin's rules for keeping them.</summary>
public sealed record GetAgendaArchiveQuery(Guid KurinKey, string? Search, int Skip, int Take)
    : IRequest<ServiceResult<AgendaArchivePageDto>>;

public sealed class GetAgendaArchiveQueryHandler : IRequestHandler<GetAgendaArchiveQuery, ServiceResult<AgendaArchivePageDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IOfficeDirectory _offices;
    private readonly IKurinDirectory _kurins;
    private readonly IKurinTagDirectory _tags;
    private readonly IAgendaAccess _access;
    private readonly UserManager<AppUser> _userManager;

    public GetAgendaArchiveQueryHandler(IUnitOfWork uow, IMemberDirectory members, IOfficeDirectory offices, IKurinDirectory kurins, IKurinTagDirectory tags, IAgendaAccess access, UserManager<AppUser> userManager)
    {
        _uow = uow;
        _members = members;
        _offices = offices;
        _kurins = kurins;
        _tags = tags;
        _access = access;
        _userManager = userManager;
    }

    public async Task<ServiceResult<AgendaArchivePageDto>> Handle(GetAgendaArchiveQuery request, CancellationToken cancellationToken)
    {
        var policy = await _kurins.GetTaskArchivePolicyAsync(request.KurinKey, cancellationToken);
        if (policy is null)
        {
            return ServiceResult<AgendaArchivePageDto>.Failure(ResultType.NotFound, AgendaErrors.KurinNotFound, "Kurin was not found.");
        }

        var viewer = await _access.BuildViewerAsync(request.KurinKey, cancellationToken);
        var items = (await _uow.AgendaItems.GetForViewerAsync(viewer.ToScope(), null, null, onlyDated: false, kind: AgendaItemKind.Task, cancellationToken, archived: true))
            .OrderByDescending(item => item.ArchivedAtUtc)
            .ToList();

        var lookups = await AgendaNames.LoadAsync(_uow, _members, _offices, _kurins, _tags, request.KurinKey, items, cancellationToken);
        var roster = await AgendaRosterRules.LoadAsync(_offices, _tags, lookups.MemberGroups, items, cancellationToken);
        var userNames = await AgendaCreatorNames.ResolveAsync(_userManager, lookups.CreatorNames, items, cancellationToken);

        var matching = items
            .Select(item => AgendaItemMapping.Create(item, viewer, AgendaNames.KurinLabel, lookups.GroupNames, lookups.MemberNames, userNames, lookups.TargetLabels, lookups.Categories, roster))
            .Where(response => AgendaSearchRules.Matches(response, request.Search))
            .ToList();

        return new ServiceResult<AgendaArchivePageDto>(ResultType.Success, new AgendaArchivePageDto
        {
            Total = matching.Count,
            Items = matching.Skip(PagingRules.Skip(request.Skip)).Take(PagingRules.Take(request.Take, AgendaBoardPagingRules.MaxTake)).ToList(),
            Policy = new AgendaArchivePolicyDto { AutoArchiveAfterDays = policy.AutoArchiveAfterDays, PurgeAfterDays = policy.PurgeAfterDays }
        });
    }
}
