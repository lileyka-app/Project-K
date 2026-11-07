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

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Archive;

/// <summary>The archived tasks the viewer may see, newest first, with the kurin's rules for keeping them.</summary>
public sealed record GetAgendaArchiveQuery(Guid KurinKey, string? Search, int Skip, int Take)
    : IRequest<ServiceResult<AgendaArchivePageResponse>>;

public sealed class GetAgendaArchiveQueryHandler : IRequestHandler<GetAgendaArchiveQuery, ServiceResult<AgendaArchivePageResponse>>
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

    public async Task<ServiceResult<AgendaArchivePageResponse>> Handle(GetAgendaArchiveQuery request, CancellationToken cancellationToken)
    {
        var kurin = await _uow.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return ServiceResult<AgendaArchivePageResponse>.Failure(ResultType.NotFound, "KURIN_NOT_FOUND", "Kurin was not found.");
        }

        var viewer = await _access.BuildViewerAsync(request.KurinKey, cancellationToken);
        var items = (await _uow.AgendaItems.GetForViewerAsync(viewer.ToScope(), null, null, onlyDated: false, kind: AgendaItemKind.Task, cancellationToken, archived: true))
            .OrderByDescending(item => item.ArchivedAtUtc)
            .ToList();

        var lookups = await AgendaLookups.LoadAsync(_uow, _members, request.KurinKey, items, cancellationToken);
        var roster = await AgendaRoster.LoadAsync(_uow, lookups.MemberGroups, items, cancellationToken);
        var userNames = await AgendaCreatorNames.ResolveAsync(_userManager, lookups.CreatorNames, items, cancellationToken);

        var matching = items
            .Select(item => AgendaItemResponseFactory.Create(item, viewer, AgendaLookups.KurinLabel, lookups.GroupNames, lookups.MemberNames, userNames, lookups.TargetLabels, lookups.Categories, roster))
            .Where(response => AgendaSearch.Matches(response, request.Search))
            .ToList();

        return new ServiceResult<AgendaArchivePageResponse>(ResultType.Success, new AgendaArchivePageResponse
        {
            Total = matching.Count,
            Items = matching.Skip(Math.Max(0, request.Skip)).Take(Math.Clamp(request.Take, 1, AgendaBoardPaging.MaxTake)).ToList(),
            Policy = new AgendaArchivePolicyDto { AutoArchiveAfterDays = kurin.TaskAutoArchiveAfterDays, PurgeAfterDays = kurin.TaskArchivePurgeAfterDays }
        });
    }
}
