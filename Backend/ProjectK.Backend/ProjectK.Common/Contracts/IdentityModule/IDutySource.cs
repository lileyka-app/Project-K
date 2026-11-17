using ProjectK.Common.Records.IdentityModule;

namespace ProjectK.Common.Contracts.IdentityModule;

/// <summary>
/// A module that gives the провід things to do: вмілості to confirm, money to acknowledge, events to
/// mark. The identity module collects every source for «Справи», one kurin at a time, and hides what
/// the person dismissed; a new module with duties adds its own source and nothing else changes.
/// <para>
/// The viewer arrives with the kurin, the agenda's view of the person there and their rights there
/// already worked out, so a source never reads the person's offices again. A source answers only what
/// those rights let the person act on, so a duty never leads to a page that would refuse them.
/// </para>
/// </summary>
public interface IDutySource
{
    /// <summary>What waits on the viewer in their kurin from this source; empty when nothing does or nothing is theirs to do.</summary>
    Task<IReadOnlyList<PendingDuty>> GetDutiesAsync(DutyViewer viewer, CancellationToken cancellationToken = default);
}
