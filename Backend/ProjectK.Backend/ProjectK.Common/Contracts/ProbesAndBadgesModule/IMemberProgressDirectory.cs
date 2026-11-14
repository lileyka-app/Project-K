using ProjectK.Common.Records.ProbesAndBadgesModule;

namespace ProjectK.Common.Contracts.ProbesAndBadgesModule;

/// <summary>
/// What the probes and вмілості module will tell others about a person. It is the whole of the way
/// in: nobody outside reads <c>ProbeProgress</c> or <c>BadgeProgress</c> for someone else's sake.
/// <para>
/// The member's dossier is composed through this rather than joined to it. A join would read another
/// module's tables in the same query and could never be answered over a network; a call can.
/// </para>
/// </summary>
public interface IMemberProgressDirectory
{
    /// <summary>Everything one person has taken and earned, in one call.</summary>
    Task<MemberProgress> GetForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>How many вмілості of these people are handed in and waiting to be confirmed.</summary>
    Task<int> CountSubmittedBadgesAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default);

    /// <summary>The ids of the points of one проба the person has had signed; empty when none.</summary>
    Task<IReadOnlyCollection<string>> GetSignedPointIdsAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// How much there is, without reading it. The dossier index says what a folder holds before
    /// anyone asks to open it.
    /// </summary>
    Task<int> CountForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everything earned in one kurin that is worth points: вмілості confirmed, points of проби
    /// signed, проби verified — each with its day. One call for the whole kurin.
    /// </summary>
    Task<KurinProgressFacts> GetFactsForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// What the звіт куреня prints for each of these people, keyed by member. One call for the
    /// whole roster: the report used to read the progress tables itself, which was the last place
    /// outside this module to do so.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, MemberProgressDetail>> GetDetailsForMembersAsync(
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default);
}
