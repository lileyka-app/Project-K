using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.ScoreModule;

public class ScoreAttendanceRepository : BaseEntityRepository<ScoreAttendance>, IScoreAttendanceRepository
{
    public ScoreAttendanceRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ScoreAttendance>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(e => e.KurinKey == kurinKey && e.RemovedAtUtc == null)
            .OrderBy(e => e.OccurrenceStartUtc)
            .ToListAsync(cancellationToken);

    public Task<ScoreAttendance?> GetStandingAsync(Guid membershipKey, Guid agendaItemKey, DateTime occurrenceStartUtc, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(
            e => e.MembershipKey == membershipKey
                && e.AgendaItemKey == agendaItemKey
                && e.OccurrenceStartUtc == occurrenceStartUtc
                && e.RemovedAtUtc == null,
            cancellationToken);

    public Task<bool> AnyAtOccurrenceAsync(Guid agendaItemKey, DateTime occurrenceStartUtc, CancellationToken cancellationToken = default)
        => Set.AnyAsync(
            e => e.AgendaItemKey == agendaItemKey
                && e.OccurrenceStartUtc == occurrenceStartUtc
                && e.RemovedAtUtc == null,
            cancellationToken);

    /// <summary>A mark is taken off, never removed: the trail needs the row.</summary>
    public override void Delete(ScoreAttendance entity, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Attendance is marked removed, never deleted.");
}
