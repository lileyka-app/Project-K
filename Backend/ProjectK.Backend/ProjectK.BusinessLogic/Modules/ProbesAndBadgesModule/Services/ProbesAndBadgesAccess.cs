using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Rules.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

public sealed class ProbesAndBadgesAccess : IProbesAndBadgesAccess
{
    private readonly IMemberProbeLine _line;
    private readonly IMemberDirectory _members;
    private readonly ICurrentUserContext _currentUser;
    private readonly IResourceAccessService _access;

    public ProbesAndBadgesAccess(
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
