using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <inheritdoc />
public sealed class KurinDirectory : IKurinDirectory
{
    private readonly IUnitOfWork _unitOfWork;

    public KurinDirectory(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetGroupNamesAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => (await _unitOfWork.Groups.GetAllAsync(kurinKey, cancellationToken))
            .ToDictionary(group => group.GroupKey, group => group.Name);

    public async Task<KurinTaskArchivePolicy?> GetTaskArchivePolicyAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await _unitOfWork.Kurins.GetByKeyAsync(kurinKey, cancellationToken) is { } kurin
            ? new KurinTaskArchivePolicy(kurin.TaskAutoArchiveAfterDays, kurin.TaskArchivePurgeAfterDays)
            : null;
}
