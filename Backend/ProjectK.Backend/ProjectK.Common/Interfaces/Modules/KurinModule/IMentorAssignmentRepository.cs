using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Interfaces.Modules.KurinModule;

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
}
