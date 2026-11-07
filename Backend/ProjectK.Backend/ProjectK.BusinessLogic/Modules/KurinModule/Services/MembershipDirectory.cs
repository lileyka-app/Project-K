using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Records;

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

    public Task<IReadOnlyCollection<KurinMembershipRecord>> GetInKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Memberships.GetRecordsInKurinAsync(kurinKey, cancellationToken);
}
