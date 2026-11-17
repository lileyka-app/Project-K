using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Records.AuthModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <inheritdoc />
public sealed class OfficeDirectory : IOfficeDirectory
{
    private readonly IUnitOfWork _unitOfWork;

    public OfficeDirectory(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<LeadershipHistoryDto>> GetHeldByMemberAsync(
        Guid memberKey,
        CancellationToken cancellationToken = default)
    {
        var histories = await _unitOfWork.Leaderships.GetHistoriesForMemberAsync(memberKey, cancellationToken);
        // Dissolved гуртки too: a person's history still names the гурток they led.
        var groups = (await _unitOfWork.Groups.GetByKeysIncludingDissolvedAsync(
                histories.Select(history => history.Leadership?.GroupKey).OfType<Guid>().Distinct().ToArray(),
                cancellationToken))
            .ToDictionary(group => group.GroupKey);
        return histories.Select(history => ToCardDto(history, groups)).ToList();
    }

    /// <summary>An office the person held, named by its провід: the type, the гурток and the kurin it is in.</summary>
    private static LeadershipHistoryDto ToCardDto(LeadershipHistory history, IReadOnlyDictionary<Guid, Group> groups)
    {
        var group = history.Leadership?.GroupKey is { } groupKey ? groups.GetValueOrDefault(groupKey) : null;
        return new LeadershipHistoryDto
        {
            LeadershipHistoryKey = history.LeadershipHistoryKey,
            MemberKey = history.MemberKey,
            LeadershipKey = history.LeadershipKey,
            Role = history.Role,
            LeadershipType = history.Leadership?.Type,
            GroupName = group?.Name,
            KurinKey = history.Leadership?.KurinKey ?? group?.KurinKey,
            StartDate = history.StartDate,
            EndDate = history.EndDate
        };
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
