using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Records.MemberModule;

namespace ProjectK.Common.Contracts.MemberModule;

public interface IMentorAssignmentRepository : IBaseEntityRepository<MentorAssignment>
{
    Task<IEnumerable<MentorAssignment>> GetByMentorUserKeyAsync(Guid mentorUserKey, CancellationToken cancellationToken = default);
    Task<IEnumerable<MentorAssignment>> GetByGroupKeyAsync(Guid groupKey, CancellationToken cancellationToken = default);
    Task<IEnumerable<MentorAssignment>> GetByKurinKeyAsync(Guid kurinKey, CancellationToken cancellationToken = default);
    /// <summary>
    /// The assignment of one mentor to one group. A pair can hold several rows, because revoking
    /// keeps the old one as history: the active assignment wins, otherwise the latest revoked one.
    /// </summary>
    Task<MentorAssignment?> GetSpecificAssignmentAsync(Guid mentorUserKey, Guid groupKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The гуртки this account runs as виховник in one kurin right now, by name. Revoked
    /// assignments are history and do not count.
    /// </summary>
    Task<IReadOnlyList<GroupRef>> GetActiveGroupsAsync(Guid mentorUserKey, Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>Whether this account runs any гурток of the kurin as виховник right now.</summary>
    Task<bool> HasActiveInKurinAsync(Guid mentorUserKey, Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The people who run a гурток of the kurin as виховник right now, once each and without an
    /// office: whether the assignment counts as one is <c>MentorOfficeRules</c>' to say.
    /// </summary>
    Task<IReadOnlyList<MemberLookupRow>> GetActiveMentorsInKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
