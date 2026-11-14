using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda;

/// <summary>The archived tasks the viewer may see, newest first, with the kurin's rules for keeping them.</summary>
public sealed record GetAgendaArchiveQuery(Guid KurinKey, string? Search, int Skip, int Take)
    : IRequest<ServiceResult<AgendaArchivePageDto>>;

public sealed class GetAgendaArchiveQueryHandler : IRequestHandler<GetAgendaArchiveQuery, ServiceResult<AgendaArchivePageDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IAgendaAccess _access;
    private readonly UserManager<AppUser> _userManager;

    public GetAgendaArchiveQueryHandler(IUnitOfWork uow, IMemberDirectory members, IAgendaAccess access, UserManager<AppUser> userManager)
    {
        _uow = uow;
        _members = members;
        _access = access;
        _userManager = userManager;
    }

    public async Task<ServiceResult<AgendaArchivePageDto>> Handle(GetAgendaArchiveQuery request, CancellationToken cancellationToken)
    {
        var kurin = await _uow.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return ServiceResult<AgendaArchivePageDto>.Failure(ResultType.NotFound, AgendaErrors.KurinNotFound, "Kurin was not found.");
        }

        var viewer = await _access.BuildViewerAsync(request.KurinKey, cancellationToken);
        var items = (await _uow.AgendaItems.GetForViewerAsync(viewer.ToScope(), null, null, onlyDated: false, kind: AgendaItemKind.Task, cancellationToken, archived: true))
            .OrderByDescending(item => item.ArchivedAtUtc)
            .ToList();

        var lookups = await AgendaNames.LoadAsync(_uow, _members, request.KurinKey, items, cancellationToken);
        var roster = await AgendaRoster.LoadAsync(_uow, lookups.MemberGroups, items, cancellationToken);
        var userNames = await AgendaCreatorNames.ResolveAsync(_userManager, lookups.CreatorNames, items, cancellationToken);

        var matching = items
            .Select(item => AgendaItemMapping.Create(item, viewer, AgendaNames.KurinLabel, lookups.GroupNames, lookups.MemberNames, userNames, lookups.TargetLabels, lookups.Categories, roster))
            .Where(response => AgendaSearchRules.Matches(response, request.Search))
            .ToList();

        return new ServiceResult<AgendaArchivePageDto>(ResultType.Success, new AgendaArchivePageDto
        {
            Total = matching.Count,
            Items = matching.Skip(PagingRules.Skip(request.Skip)).Take(PagingRules.Take(request.Take, AgendaBoardPagingRules.MaxTake)).ToList(),
            Policy = new AgendaArchivePolicyDto { AutoArchiveAfterDays = kurin.TaskAutoArchiveAfterDays, PurgeAfterDays = kurin.TaskArchivePurgeAfterDays }
        });
    }
}
