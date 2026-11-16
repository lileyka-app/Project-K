using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>
/// The probes and вмілості module's rights: who may write a third-probe project, and which probe has
/// projects at all.
/// <para>
/// Writing is for the person themself — it is their project — and for whoever the kurin's
/// «Права й видимість» lets sign that person's probe (<see cref="KurinCapability.SignProbe"/>):
/// by default the Звʼязковий in the whole kurin and the впорядник in their own гурток. Someone
/// who may only read the probe reads the projects too, and nothing more.
/// </para>
/// </summary>
public interface IProbesAndBadgesAccess
{
    /// <summary>Whether <paramref name="probeId"/> stands at the third place of the person's line.</summary>
    Task<bool> IsProjectProbeAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default);

    Task<bool> CanEditAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms the reflection, a project's start or its completion, and sets the stages:
    /// <see cref="KurinCapability.SignProbe"/>, never on one's own probe.
    /// </summary>
    Task<bool> CanSignAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>Takes such a confirmation back: <see cref="KurinCapability.UnsignProbe"/>, never on one's own probe.</summary>
    Task<bool> CanUnsignAsync(Guid memberKey, CancellationToken cancellationToken = default);
}
