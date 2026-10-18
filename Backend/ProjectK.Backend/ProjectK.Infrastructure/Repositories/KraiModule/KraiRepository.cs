using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KraiModule;

public class KraiRepository : IKraiRepository
{
    private readonly AppDbContext _context;

    public KraiRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Krai>> GetAllAsync(CancellationToken token = default)
    {
        return await _context.Krais.AsNoTracking().OrderBy(k => k.Code).ToListAsync(token);
    }

    public Task<bool> ExistsAsync(string code, CancellationToken token = default)
    {
        return _context.Krais.AnyAsync(k => k.Code == code, token);
    }

    public Task<Krai?> GetAsync(string code, CancellationToken token = default)
    {
        return _context.Krais.AsNoTracking().FirstOrDefaultAsync(k => k.Code == code, token);
    }

    public Task<Krai?> GetForUpdateAsync(string code, CancellationToken token = default)
    {
        return _context.Krais.FirstOrDefaultAsync(k => k.Code == code, token);
    }

    public void Add(Krai krai)
    {
        _context.Krais.Add(krai);
    }

    public Task<KraiProfileRevision?> GetLatestProfileRevisionAsync(string code, CancellationToken token = default)
    {
        return _context.KraiProfileRevisions
            .AsNoTracking()
            .Where(r => r.KraiCode == code)
            .OrderByDescending(r => r.Revision)
            .FirstOrDefaultAsync(token);
    }

    public Task<KraiProfileRevision?> GetProfileRevisionAsync(string code, int revision, CancellationToken token = default)
    {
        return _context.KraiProfileRevisions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.KraiCode == code && r.Revision == revision, token);
    }

    public async Task<IReadOnlyList<KraiProfileRevision>> GetProfileRevisionsAsync(string code, CancellationToken token = default)
    {
        return await _context.KraiProfileRevisions
            .AsNoTracking()
            .Where(r => r.KraiCode == code)
            .OrderByDescending(r => r.Revision)
            .ToListAsync(token);
    }

    public void AddProfileRevision(KraiProfileRevision revision)
    {
        _context.KraiProfileRevisions.Add(revision);
    }
}
