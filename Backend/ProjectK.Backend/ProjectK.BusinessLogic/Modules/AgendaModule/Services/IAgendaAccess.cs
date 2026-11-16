using ProjectK.Common.Dtos.AgendaModule.Requests;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Records.AgendaModule;
using ProjectK.Common.Records.InfrastructureModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Services;

public interface IAgendaAccess
{
    /// <summary>Resolves the current user's agenda context inside <paramref name="kurinKey"/>.</summary>
    Task<AgendaViewerContext> BuildViewerAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the current user may assign an item to <paramref name="target"/>: the kurin's policy
    /// row for that kind of target (<see cref="KurinCapability.AssignKurin"/>, <c>AssignGroups</c>,
    /// <c>AssignMembers</c>, <c>AssignProvid</c>), checked against the target's scope by the resource
    /// guard — which also refuses a target outside the user's kurin.
    /// </summary>
    Task<ResourceAccessDecision> AuthorizeTargetAsync(
        AgendaTargetInput target,
        CancellationToken cancellationToken = default);

    /// <summary>Whether the current user may author agenda items at all: some kind of target is within reach.</summary>
    Task<bool> CanAuthorAsync(CancellationToken cancellationToken = default);

    /// <summary>The current user's assigning reach inside <paramref name="kurinKey"/>, for deciding many targets at once.</summary>
    Task<AgendaAssignReach> GetAssignReachAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
