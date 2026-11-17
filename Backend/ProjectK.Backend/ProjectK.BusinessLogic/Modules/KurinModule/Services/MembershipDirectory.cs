using ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <inheritdoc />
public sealed class MembershipDirectory : IMembershipDirectory
{
    private readonly IUnitOfWork _unitOfWork;

    public MembershipDirectory(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public Task<IReadOnlyCollection<MembershipRecord>> GetForMemberAsync(
        Guid memberKey,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Memberships.GetRecordsForMemberAsync(memberKey, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<MembershipRecord>>> GetForMembersAsync(
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Memberships.GetRecordsForMembersAsync(memberKeys, cancellationToken);

    public Task<int> CountForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => _unitOfWork.Memberships.CountForMemberAsync(memberKey, cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetCurrentKurinKeysForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => (await _unitOfWork.Memberships.GetActiveForMemberAsync(memberKey, cancellationToken))
            .Select(m => m.KurinKey)
            .Distinct()
            .ToList();

    public Task<IReadOnlyCollection<Guid>> GetKurinKeysForAccountAsync(
        Guid userKey,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Memberships.GetKurinKeysForAccountAsync(userKey, cancellationToken);

    public Task<IReadOnlyCollection<MembershipRecord>> GetCurrentForAccountAsync(
        Guid userKey,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Memberships.GetCurrentRecordsForAccountAsync(userKey, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<MembershipRecord>>> GetCurrentForAccountsAsync(
        IReadOnlyCollection<Guid> userKeys,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Memberships.GetCurrentRecordsForAccountsAsync(userKeys, cancellationToken);

    public Task<IReadOnlyCollection<Guid>> GetAccountKeysInKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Memberships.GetAccountKeysInKurinAsync(kurinKey, cancellationToken);

    public Task<int> CountCurrentInKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => _unitOfWork.Memberships.CountCurrentInKurinAsync(kurinKey, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, int>> CountCurrentInKurinsAsync(
        IReadOnlyCollection<Guid> kurinKeys,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Memberships.CountCurrentInKurinsAsync(kurinKeys, cancellationToken);

    public Task<IReadOnlyCollection<KurinMembershipRecord>> GetInKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Memberships.GetRecordsInKurinAsync(kurinKey, cancellationToken);

    public async Task<IReadOnlyCollection<MemberLookupDto>> GetLookupByKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default)
        => (await _unitOfWork.Memberships.GetMentorCandidatesAsync(kurinKey, cancellationToken))
            .Select(candidate => candidate.ToLookupDto())
            .ToList();
}
