using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>
/// Who may write a third-probe project, and which probe has projects at all.
/// <para>
/// Writing is for the person themself — it is their project — and for whoever the kurin's
/// «Права й видимість» lets sign that person's probe (<see cref="KurinCapability.SignProbe"/>):
/// by default the Звʼязковий in the whole kurin and the впорядник in their own гурток. Someone
/// who may only read the probe reads the projects too, and nothing more.
/// </para>
/// </summary>
public interface IProbeProjectAccess
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

public sealed class ProbeProjectAccess : IProbeProjectAccess
{
    private readonly IMemberProbeLine _line;
    private readonly IMemberDirectory _members;
    private readonly ICurrentUserContext _currentUser;
    private readonly IResourceAccessService _access;

    public ProbeProjectAccess(
        IMemberProbeLine line,
        IMemberDirectory members,
        ICurrentUserContext currentUser,
        IResourceAccessService access)
    {
        _line = line;
        _members = members;
        _currentUser = currentUser;
        _access = access;
    }

    public async Task<bool> IsProjectProbeAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default)
    {
        var line = await _line.ForMemberAsync(memberKey, cancellationToken);
        return line.Any(probe => probe.Order == ProbeProjectRules.ProbeOrder
                                 && string.Equals(probe.Id, probeId, StringComparison.Ordinal));
    }

    public async Task<bool> CanEditAsync(Guid memberKey, CancellationToken cancellationToken = default) =>
        await IsSelfAsync(memberKey, cancellationToken)
        || (await _access.CheckCapabilityAsync(KurinCapability.SignProbe, ResourceType.Member, memberKey, cancellationToken)).IsAllowed;

    // Nobody signs their own probe: the access service refuses SignProbe and UnsignProbe on one's own record.
    public async Task<bool> CanSignAsync(Guid memberKey, CancellationToken cancellationToken = default) =>
        (await _access.CheckCapabilityAsync(KurinCapability.SignProbe, ResourceType.Member, memberKey, cancellationToken)).IsAllowed;

    public async Task<bool> CanUnsignAsync(Guid memberKey, CancellationToken cancellationToken = default) =>
        (await _access.CheckCapabilityAsync(KurinCapability.UnsignProbe, ResourceType.Member, memberKey, cancellationToken)).IsAllowed;

    private async Task<bool> IsSelfAsync(Guid memberKey, CancellationToken cancellationToken) =>
        _currentUser.UserId is { } userKey
        && await _members.FindAccountKeyAsync(memberKey, cancellationToken) == userKey;
}
