using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Records.AuthModule;
using ProjectK.Common.Rules.MemberModule;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Services;

/// <inheritdoc />
public sealed class OfficeDirectory : IOfficeDirectory
{
    private readonly IUnitOfWork _unitOfWork;

    public OfficeDirectory(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyCollection<MemberOffice>> GetForAccountInKurinAsync(
        Guid userKey,
        Guid kurinKey,
        CancellationToken cancellationToken = default)
        => MentorOfficeRules.WithAssignment(
            await _unitOfWork.Leaderships.GetActiveOfficesForAccountInKurinAsync(userKey, kurinKey, cancellationToken),
            await _unitOfWork.MentorAssignments.HasActiveInKurinAsync(userKey, kurinKey, cancellationToken));

    public Task<IReadOnlyList<Guid>> GetLeadershipKeysForMemberAsync(
        Guid memberKey,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Leaderships.GetActiveLeadershipKeysForMemberAsync(memberKey, cancellationToken);

    public Task<IReadOnlyList<LeadershipRef>> GetLeadershipsInKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Leaderships.GetLeadershipRefsForKurinAsync(kurinKey, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetHolderMemberKeysAsync(
        IReadOnlyCollection<Guid> leadershipKeys,
        CancellationToken cancellationToken = default)
    {
        var holders = new Dictionary<Guid, IReadOnlyList<Guid>>();
        foreach (var leadershipKey in leadershipKeys.Distinct())
        {
            holders[leadershipKey] = await _unitOfWork.Leaderships.GetActiveMemberKeysForLeadershipAsync(leadershipKey, cancellationToken);
        }

        return holders;
    }

    public Task<IReadOnlyList<Guid>> GetHolderAccountKeysAsync(
        Guid leadershipKey,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Leaderships.GetActiveMemberUserKeysForLeadershipAsync(leadershipKey, cancellationToken);
}
