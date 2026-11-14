using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <inheritdoc />
public sealed class GroupProbeEditions : IGroupProbeEditions
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMembershipDirectory _memberships;

    public GroupProbeEditions(IUnitOfWork unitOfWork, IMembershipDirectory memberships)
    {
        _unitOfWork = unitOfWork;
        _memberships = memberships;
    }

    public async Task<string?> ForMemberAsync(Guid memberKey, Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var membership = (await _memberships.GetForMemberAsync(memberKey, cancellationToken))
            .FirstOrDefault(record => record.KurinKey == kurinKey && record.IsCurrent);
        if (membership?.GroupKey is not { } groupKey)
        {
            return null;
        }

        var group = await _unitOfWork.Groups.GetByKeyAsync(groupKey, cancellationToken);
        return group?.ProbeEdition;
    }
}
