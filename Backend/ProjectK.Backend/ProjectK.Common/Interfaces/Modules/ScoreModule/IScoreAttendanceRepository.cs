using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Common.Interfaces.Modules.ScoreModule;

public interface IScoreAttendanceRepository : IBaseEntityRepository<ScoreAttendance>
{
    /// <summary>Every mark still standing in the kurin, untracked.</summary>
    Task<IReadOnlyList<ScoreAttendance>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>The mark standing for that person at that occurrence, tracked — or null.</summary>
    Task<ScoreAttendance?> GetStandingAsync(Guid membershipKey, Guid agendaItemKey, DateTime occurrenceStartUtc, CancellationToken cancellationToken = default);

    /// <summary>Whether anyone's mark stands at that occurrence.</summary>
    Task<bool> AnyAtOccurrenceAsync(Guid agendaItemKey, DateTime occurrenceStartUtc, CancellationToken cancellationToken = default);
}
