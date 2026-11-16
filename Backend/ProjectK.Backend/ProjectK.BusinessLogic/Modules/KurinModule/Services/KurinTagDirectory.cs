using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <inheritdoc />
public sealed class KurinTagDirectory : IKurinTagDirectory
{
    private readonly IKurinTagVisibility _visibility;
    private readonly IUnitOfWork _unitOfWork;

    public KurinTagDirectory(IKurinTagVisibility visibility, IUnitOfWork unitOfWork)
    {
        _visibility = visibility;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> IsEnabledAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => (await _visibility.SettingsAsync(kurinKey, cancellationToken))?.Enabled == true;

    public Task<CapabilityReach> GetCarrierReachAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => _visibility.CarrierReachAsync(kurinKey, cancellationToken);

    public async Task<IReadOnlyList<KurinTagSummary>> GetTagsAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => (await _unitOfWork.KurinTags.GetForKurinAsync(kurinKey, cancellationToken))
            .Select(tag => new KurinTagSummary(tag.KurinTagKey, tag.Name, tag.ColorHex))
            .ToList();

    public async Task<IReadOnlyList<Guid>> GetTagKeysOfMemberAsync(Guid kurinKey, Guid memberKey, CancellationToken cancellationToken = default)
        => (await _unitOfWork.KurinTags.GetAssignmentsAsync(kurinKey, [memberKey], cancellationToken))
            .Select(assignment => assignment.KurinTagKey)
            .ToList();

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetCarriersAsync(
        IReadOnlyCollection<Guid> kurinTagKeys,
        CancellationToken cancellationToken = default)
    {
        var carriers = new Dictionary<Guid, IReadOnlyList<Guid>>();
        foreach (var kurinTagKey in kurinTagKeys.Distinct())
        {
            carriers[kurinTagKey] = await _unitOfWork.KurinTags.GetMemberKeysAsync(kurinTagKey, cancellationToken);
        }

        return carriers;
    }
}
