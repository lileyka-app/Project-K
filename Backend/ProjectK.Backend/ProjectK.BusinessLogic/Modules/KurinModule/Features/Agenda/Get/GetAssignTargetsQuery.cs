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
        // The same reach the create handler checks target by target, worked out once for the tree,
        // so the tree never offers what a save would refuse.
        var reach = await _access.GetAssignReachAsync(request.KurinKey, cancellationToken);
        if (!reach.CanAuthor)
        {
            return ServiceResult<AgendaAssignTargetsResponse>.Failure(
                ResultType.Forbidden, "AGENDA_AUTHOR_FORBIDDEN", "The kurin's policy lets this user assign nothing.");
        }

        var groups = (await _uow.Groups.GetAllAsync(request.KurinKey, cancellationToken)).ToList();
        var members = await _members.GetByKurinAsync(request.KurinKey, cancellationToken);
        var groupNames = groups.ToDictionary(g => g.GroupKey, g => g.Name);

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
                CanTarget = reach.ReachesProvid(null)
            })
            .ToList();

        var groupTargets = groups
            .OrderBy(g => g.Name)
            .Select(g => new AgendaGroupTargetDto
            {
                GroupKey = g.GroupKey,
                Name = g.Name,
                CanTargetGroup = reach.ReachesGroup(g.GroupKey),
                Leadership = groupLeaderships.TryGetValue(g.GroupKey, out var office)
                    ? new AgendaLeadershipTargetDto
                    {
                        LeadershipKey = office.LeadershipKey,
                        Label = AgendaLookups.LabelFor(office.Type, office.GroupKey, groupNames),
                        CanTarget = reach.ReachesProvid(g.GroupKey)
                    }
                    : null,
                Members = members
                    .Where(m => m.GroupKey == g.GroupKey)
                    .OrderBy(m => m.LastName)
                    .Select(m => new AgendaMemberTargetDto
                    {
                        MemberKey = m.MemberKey,
                        FullName = $"{m.FirstName} {m.LastName}".Trim(),
                        CanTarget = reach.ReachesMember(m.GroupKey, m.UserKey)
                    })
                    .ToList()
            })
            .Where(g => g.CanTargetGroup || g.Leadership?.CanTarget == true || g.Members.Any(m => m.CanTarget))
            .ToList();

        var response = new AgendaAssignTargetsResponse
        {
            CanTargetKurin = reach.ReachesKurin,
            KurinKey = request.KurinKey,
            KurinLabel = AgendaLookups.KurinLabel,
            KurinLeaderships = kurinLeaderships,
            Groups = groupTargets
        };

        return new ServiceResult<AgendaAssignTargetsResponse>(ResultType.Success, response);
    }
}
