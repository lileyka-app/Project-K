using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Stores;

/// <inheritdoc />
public sealed class RefreshTokenStore : IRefreshTokenStore
{
    private readonly AppDbContext _context;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly TimeProvider _timeProvider;

    public RefreshTokenStore(
        AppDbContext context,
        DbContextOptions<AppDbContext> options,
        TimeProvider timeProvider)
    {
        _context = context;
        _options = options;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Written through a context of its own.
    /// <para>
    /// Handing out a session is its own fact, not part of whatever the caller is composing — and the
    /// scoped <see cref="AppDbContext"/> is shared with <c>IUnitOfWork</c>, so saving through it here
    /// would commit a half-built unit of work that the caller had not finished and could no longer
    /// roll back. The other operations only ever touch session rows, so they can use the shared one.
    /// </para>
    /// </summary>
    public async Task IssueAsync(
        Guid userId,
        string token,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var context = new AppDbContext(_options);

        context.UserRefreshTokens.Add(new UserRefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAtUtc = expiresAtUtc
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<UserRefreshToken?> FindActiveAsync(string token, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        return _context.UserRefreshTokens
            .FirstOrDefaultAsync(
                session => session.Token == token
                    && session.RevokedAtUtc == null
                    && session.ExpiresAtUtc > now,
                cancellationToken);
    }

    public async Task<bool> RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // One statement, so the "still active?" test and the write cannot be separated: whoever the
        // database counts as having updated the row is the one that spent the token.
        var revoked = await _context.UserRefreshTokens
            .Where(session => session.Token == token && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                session => session.SetProperty(row => row.RevokedAtUtc, now),
                cancellationToken);

        return revoked > 0;
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.UserRefreshTokens
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                session => session.SetProperty(row => row.RevokedAtUtc, now),
                cancellationToken);
    }
}
