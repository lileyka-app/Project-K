using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.AgendaModule;

public class AgendaResponseRepository : BaseEntityRepository<AgendaResponse>, IAgendaResponseRepository
{

    public AgendaResponseRepository(AppDbContext context) : base(context)
    {
    }

    public override async Task<AgendaResponse?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default) =>
        await Context.AgendaResponses.FirstOrDefaultAsync(r => r.AgendaResponseKey == entityKey, cancellationToken);

    public override async Task<bool> ExistsAsync(Guid entityKey, CancellationToken cancellationToken = default) =>
        await Context.AgendaResponses.AnyAsync(r => r.AgendaResponseKey == entityKey, cancellationToken);

    public async Task<IReadOnlyList<AgendaResponse>> GetForItemAsync(Guid agendaItemKey, DateTime? occurrenceStartUtc, CancellationToken cancellationToken = default) =>
        await Context.AgendaResponses
            .Where(r => r.AgendaItemKey == agendaItemKey && r.OccurrenceStartUtc == occurrenceStartUtc)
            .OrderBy(r => r.RespondedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<AgendaResponse?> GetForItemAndUserAsync(Guid agendaItemKey, Guid userKey, DateTime? occurrenceStartUtc, CancellationToken cancellationToken = default) =>
        await Context.AgendaResponses.FirstOrDefaultAsync(
            r => r.AgendaItemKey == agendaItemKey && r.UserKey == userKey && r.OccurrenceStartUtc == occurrenceStartUtc,
            cancellationToken);

    public async Task<IReadOnlyList<AgendaResponse>> GetForUserAsync(Guid userKey, IReadOnlyCollection<Guid> agendaItemKeys, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        if (agendaItemKeys.Count == 0)
        {
            return Array.Empty<AgendaResponse>();
        }

        return await Context.AgendaResponses
            .Where(r => r.UserKey == userKey
                && agendaItemKeys.Contains(r.AgendaItemKey)
                && (r.OccurrenceStartUtc == null || (r.OccurrenceStartUtc >= fromUtc && r.OccurrenceStartUtc <= toUtc)))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
