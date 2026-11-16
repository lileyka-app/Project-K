using ProjectK.Common.Dtos.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>
/// A person's <see cref="ProbeLineRules"/>, read from their progress, their гурток's edition and the
/// catalogue. The one place clients, the dashboard and the guards on signing take it from.
/// </summary>
public interface IMemberProbeLine
{
    /// <summary>Empty when the member does not exist.</summary>
    Task<IReadOnlyList<ProbeSummaryDto>> ForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>Asked only for a probe the person has not started: one already under way may go on.</summary>
    Task<ProbeStart> CanStartAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default);
}
