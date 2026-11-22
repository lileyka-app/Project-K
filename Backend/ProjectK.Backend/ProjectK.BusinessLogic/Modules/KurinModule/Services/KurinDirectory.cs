using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Rules.KurinModule;

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

    public async Task<IReadOnlyDictionary<Guid, GroupSummary>> GetGroupsAsync(IReadOnlyCollection<Guid> groupKeys, CancellationToken cancellationToken = default)
        => (await _unitOfWork.Groups.GetByKeysIncludingDissolvedAsync(groupKeys, cancellationToken))
            .ToDictionary(group => group.GroupKey, group => new GroupSummary(group.GroupKey, group.KurinKey, group.Name));

    public async Task<KurinTaskArchivePolicy?> GetTaskArchivePolicyAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await _unitOfWork.Kurins.GetByKeyAsync(kurinKey, cancellationToken) is { } kurin
            ? new KurinTaskArchivePolicy(kurin.TaskAutoArchiveAfterDays, kurin.TaskArchivePurgeAfterDays)
            : null;

    public async Task<IReadOnlyDictionary<LeadershipRole, string>?> GetRoleNamesAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await _unitOfWork.Kurins.GetByKeyAsync(kurinKey, cancellationToken) is { } kurin
            ? KurinRoleNames.Read(kurin.RoleNames)
            : null;

    public async Task<bool> HasGroupsAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await _unitOfWork.Kurins.GetByKeyAsync(kurinKey, cancellationToken) is not { } kurin
            || !KurinBranchSetRules.IsSenior(kurin.Branch);

    public async Task<bool> IsScoreEnabledAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => (await _unitOfWork.Kurins.GetByKeyAsync(kurinKey, cancellationToken))?.IsScoreEnabled ?? true;

    public async Task<IReadOnlyCollection<MinorCardSharing>> GetMinorCardSharingAsync(IReadOnlyCollection<Guid> kurinKeys, CancellationToken cancellationToken = default)
        => (await _unitOfWork.KurinCardSharing.GetForKurinsAsync(kurinKeys, cancellationToken))
            .Select(s => new MinorCardSharing(s.KurinKey, s.MinorsMayShare, s.MinorFields, s.MinorsMayLink))
            .ToList();
}
