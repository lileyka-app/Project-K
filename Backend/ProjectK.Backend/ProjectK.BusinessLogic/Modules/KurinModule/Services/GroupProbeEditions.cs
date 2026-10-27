using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <inheritdoc />
public sealed class GroupProbeEditions : IGroupProbeEditions
{
    private readonly IUnitOfWork _unitOfWork;

    public GroupProbeEditions(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<string?> ForMemberAsync(Guid memberKey, Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var membership = await _unitOfWork.Memberships.GetActiveAsync(memberKey, kurinKey, cancellationToken);
        if (membership?.GroupKey is not { } groupKey)
        {
            return null;
        }

        var group = await _unitOfWork.Groups.GetByKeyAsync(groupKey, cancellationToken);
        return group?.ProbeEdition;
    }
}
