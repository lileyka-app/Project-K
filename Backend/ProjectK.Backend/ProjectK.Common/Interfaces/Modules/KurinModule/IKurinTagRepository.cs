using ProjectK.Common.Entities.KurinModule.Tags;

namespace ProjectK.Common.Interfaces.Modules.KurinModule;

public interface IKurinTagRepository : IBaseEntityRepository<KurinTag>
{
    /// <summary>The kurin's tags, by name, untracked.</summary>
    Task<IReadOnlyList<KurinTag>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>A tag with everyone who carries it, tracked, for changing who carries it or deleting it.</summary>
    Task<KurinTag?> GetWithMembersAsync(Guid kurinTagKey, CancellationToken cancellationToken = default);

    /// <summary>Whether another tag of the kurin already has this name, ignoring case.</summary>
    Task<bool> NameTakenAsync(Guid kurinKey, string name, Guid? exceptKurinTagKey, CancellationToken cancellationToken = default);

    /// <summary>Every tag on every person in the kurin now, in one read.</summary>
    Task<IReadOnlyList<MemberTag>> GetAssignmentsAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>The kurin's tags on these people, in one read.</summary>
    Task<IReadOnlyList<MemberTag>> GetAssignmentsAsync(
        Guid kurinKey,
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default);

    /// <summary>The kurin's tags on one person, tracked, for changing them.</summary>
    Task<IReadOnlyList<MemberTag>> GetTrackedForMemberAsync(Guid kurinKey, Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>How many of the kurin's tags each of these people carries now.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountPerMemberAsync(
        Guid kurinKey,
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default);

    /// <summary>Everyone carrying the tag who is in the kurin now.</summary>
    Task<IReadOnlyList<Guid>> GetMemberKeysAsync(Guid kurinTagKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Holds these people's tags for the rest of the current transaction: a second change to the tags of
    /// any of them waits until this one commits, so a count read after it is the count that stands.
    /// </summary>
    Task HoldMembersAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default);

    void AddAssignment(MemberTag assignment);
    void RemoveAssignment(MemberTag assignment);

    /// <summary>The kurin's switches, tracked; null when the kurin keeps the defaults.</summary>
    Task<KurinTagSettings?> GetSettingsAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    void AddSettings(KurinTagSettings settings);
}
