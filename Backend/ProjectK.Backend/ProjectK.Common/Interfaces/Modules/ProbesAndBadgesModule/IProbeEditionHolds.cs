using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;

/// <summary>
/// Where some people stand in the editions of the probe programme: who is held to one whatever their
/// гурток chooses (they closed a probe past the first place in it), and who only has a probe under way.
/// </summary>
public interface IProbeEditionHolds
{
    /// <summary>Only people with a probe closed or under way; someone with nothing started is absent.</summary>
    Task<IReadOnlyDictionary<Guid, ProbeEditionHold>> ForMembersAsync(
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default);
}
