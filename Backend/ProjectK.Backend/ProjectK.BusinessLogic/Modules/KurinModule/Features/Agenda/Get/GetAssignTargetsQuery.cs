using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Get;

/// <summary>The "Assign for" tree, trimmed to what the current user is allowed to target.</summary>
public sealed record GetAssignTargetsQuery(Guid KurinKey)
    : IRequest<ServiceResult<AgendaAssignTargetsResponse>>;

public sealed class GetAssignTargetsQueryHandler
    : IRequestHandler<GetAssignTargetsQuery, ServiceResult<AgendaAssignTargetsResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IAgendaAccess _access;

    public GetAssignTargetsQueryHandler(IUnitOfWork uow, IMemberDirectory members, IAgendaAccess access)
    {
        _uow = uow;
        _members = members;
        _access = access;
    }

    public async Task<ServiceResult<AgendaAssignTargetsResponse>> Handle(GetAssignTargetsQuery request, CancellationToken cancellationToken)
    {
        var viewer = await _access.BuildViewerAsync(request.KurinKey, cancellationToken);

        var kurinDecision = await _access.AuthorizeTargetAsync(
            new AgendaTargetInput { TargetType = AgendaTargetType.Kurin, TargetKey = request.KurinKey },
            ResourceAction.Create,
            cancellationToken);

        var groups = (await _uow.Groups.GetAllAsync(request.KurinKey, cancellationToken)).ToList();
        var members = await _members.GetByKurinAsync(request.KurinKey, cancellationToken);
        var groupNames = groups.ToDictionary(g => g.GroupKey, g => g.Name);

        // Whoever may aim at the whole kurin sees every group; the rest see their own and the ones they
        // lead. Each group is then asked the same question the create handler will ask, so the tree never
        // offers what a save would refuse.
        var visibleGroups = viewer.CanSeeWholeKurin || kurinDecision.IsAllowed
            ? groups
            : groups.Where(g => viewer.VisibilityGroupKeys.Contains(g.GroupKey)).ToList();

        var targetableGroups = new HashSet<Guid>();
        foreach (var groupKey in visibleGroups.Select(group => group.GroupKey))
        {
            var groupDecision = kurinDecision.IsAllowed
                ? kurinDecision
                : await _access.AuthorizeTargetAsync(
                    new AgendaTargetInput { TargetType = AgendaTargetType.Group, TargetKey = groupKey },
                    ResourceAction.Create,
                    cancellationToken);
            if (groupDecision.IsAllowed)
            {
                targetableGroups.Add(groupKey);
            }
        }

        // Проводи share the scope of what they lead: КВ/Курінний провід ⇒ kurin scope, Гуртковий провід
        // ⇒ its group. So the kurin-level offices are targetable exactly when the whole kurin is, and a
        // гуртковий провід exactly when its group is — no extra authorization round-trips needed.
        var leaderships = await _uow.Leaderships.GetLeadershipRefsForKurinAsync(request.KurinKey, cancellationToken);
        var groupLeaderships = leaderships
            .Where(l => l.Type == LeadershipType.Group && l.GroupKey.HasValue)
            .GroupBy(l => l.GroupKey!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var kurinLeaderships = leaderships
            .Where(l => l.Type is LeadershipType.KV or LeadershipType.Kurin)
            .OrderBy(l => l.Type)
            .Select(l => new AgendaLeadershipTargetDto
            {
                LeadershipKey = l.LeadershipKey,
                Label = AgendaLookups.LabelFor(l.Type, l.GroupKey, groupNames),
                CanTarget = kurinDecision.IsAllowed
            })
            .ToList();

        var response = new AgendaAssignTargetsResponse
        {
            CanTargetKurin = kurinDecision.IsAllowed,
            KurinKey = request.KurinKey,
            KurinLabel = AgendaLookups.KurinLabel,
            KurinLeaderships = kurinLeaderships,
            Groups = visibleGroups
                .OrderBy(g => g.Name)
                .Select(g => new AgendaGroupTargetDto
                {
                    GroupKey = g.GroupKey,
                    Name = g.Name,
                    CanTargetGroup = targetableGroups.Contains(g.GroupKey),
                    Leadership = groupLeaderships.TryGetValue(g.GroupKey, out var office)
                        ? new AgendaLeadershipTargetDto
                        {
                            LeadershipKey = office.LeadershipKey,
                            Label = AgendaLookups.LabelFor(office.Type, office.GroupKey, groupNames),
                            CanTarget = targetableGroups.Contains(g.GroupKey)
                        }
                        : null,
                    Members = members
                        .Where(m => m.GroupKey == g.GroupKey)
                        .OrderBy(m => m.LastName)
                        .Select(m => new AgendaMemberTargetDto
                        {
                            MemberKey = m.MemberKey,
                            FullName = $"{m.FirstName} {m.LastName}".Trim()
                        })
                        .ToList()
                })
                .ToList()
        };

        return new ServiceResult<AgendaAssignTargetsResponse>(ResultType.Success, response);
    }
}
