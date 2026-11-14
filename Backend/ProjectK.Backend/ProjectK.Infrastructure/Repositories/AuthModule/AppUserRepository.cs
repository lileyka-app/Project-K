using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.AuthModule;

public sealed class AppUserRepository : IAppUserRepository
{
    private readonly AppDbContext _context;

    public AppUserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Users.AsNoTracking().ToListAsync(cancellationToken);

    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _context.Users.FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<int> CountActiveAsync(
        IReadOnlyCollection<Guid> userKeys,
        CancellationToken cancellationToken = default)
    {
        if (userKeys.Count == 0)
        {
            return Task.FromResult(0);
        }

        var keys = userKeys.Distinct().ToList();
        return _context.Users.CountAsync(
            user => keys.Contains(user.Id) && user.OnboardingStatus == OnboardingStatus.Active,
            cancellationToken);
    }

    public async Task DetachFromKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var scoped = await _context.Users
            .Where(user => user.ActiveKurinKey == kurinKey)
            .ToListAsync(cancellationToken);

        foreach (var user in scoped)
        {
            user.ActiveKurinKey = null;
        }
    }

    public Task<Guid?> GetActiveKurinKeyAsync(Guid userKey, CancellationToken cancellationToken = default)
        => _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userKey)
            .Select(user => user.ActiveKurinKey)
            .FirstOrDefaultAsync(cancellationToken);
}
