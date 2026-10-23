using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.BackgroundServices;

public class AuditCleanupBackgroundService : BackgroundService
{
    private const int ReadNotificationRetentionDays = 7;
    private const int UnreadNotificationRetentionDays = 30;

    /// <summary>
    /// How long a dead session is kept. Long enough to answer "when did this session end", short
    /// enough that the table does not grow forever: refresh rotates on every call, so an active
    /// account leaves a spent row behind several times a day.
    /// </summary>
    private const int EndedSessionRetentionDays = 30;
    private const int RevokedCalendarFeedRetentionDays = 90;

    /// <summary>How long the kurin's journal keeps an entry: a year, the owner's decision.</summary>
    internal const int JournalRetentionDays = 365;

    /// <summary>
    /// Entries removed per statement. The first sweep after a release may face a year of entries at
    /// once, and one statement over all of them would hold its locks for as long as it takes.
    /// </summary>
    internal const int JournalDeleteBatchSize = 5000;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditCleanupBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

    public AuditCleanupBackgroundService(IServiceProvider serviceProvider, ILogger<AuditCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit Cleanup Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldRecordsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing Audit Cleanup Service.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Audit Cleanup Service is stopping.");
    }

    private async Task CleanupOldRecordsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var onboardingRetentionDate = now.AddDays(-30);
        var readNotificationRetentionDate = now.AddDays(-ReadNotificationRetentionDays);
        var unreadNotificationRetentionDate = now.AddDays(-UnreadNotificationRetentionDays);

        _logger.LogInformation("Starting database cleanup for records older than retention policies.");

        // 1. Cleanup Waitlist Entries (finished with, and > 30 days)
        var deletedWaitlistEntries = await SpentWaitlistEntries(dbContext, onboardingRetentionDate)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedWaitlistEntries > 0)
            _logger.LogInformation("Cleaned up {Count} old WaitlistEntries.", deletedWaitlistEntries);

        // 2. Cleanup expired/used Invitations (> 30 days)
        var deletedInvitations = await dbContext.Invitations
            .Where(i =>
                (i.UsedAtUtc.HasValue && i.UsedAtUtc < onboardingRetentionDate) ||
                (!i.UsedAtUtc.HasValue && i.ExpiresAtUtc < onboardingRetentionDate))
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedInvitations > 0)
            _logger.LogInformation("Cleaned up {Count} old Invitations.", deletedInvitations);

        // 3. Cleanup read notifications (> 7 days after read)
        var deletedReadNotifications = await dbContext.AppNotifications
            .Where(n => n.ReadAtUtc.HasValue && n.ReadAtUtc < readNotificationRetentionDate)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedReadNotifications > 0)
            _logger.LogInformation("Cleaned up {Count} old read AppNotifications.", deletedReadNotifications);

        // 4. Cleanup unread notifications (> 30 days after creation)
        var deletedUnreadNotifications = await dbContext.AppNotifications
            .Where(n => !n.ReadAtUtc.HasValue && n.CreatedAtUtc < unreadNotificationRetentionDate)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedUnreadNotifications > 0)
            _logger.LogInformation("Cleaned up {Count} old unread AppNotifications.", deletedUnreadNotifications);

        // 5. Cleanup ended sessions: revoked a while ago, or expired on their own. A live session
        // is never touched — only rows that can no longer authenticate anyone.
        var sessionRetentionDate = now.AddDays(-EndedSessionRetentionDays);
        var deletedSessions = await dbContext.UserRefreshTokens
            .Where(session =>
                (session.RevokedAtUtc.HasValue && session.RevokedAtUtc < sessionRetentionDate) ||
                session.ExpiresAtUtc < sessionRetentionDate)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedSessions > 0)
            _logger.LogInformation("Cleaned up {Count} ended UserRefreshTokens.", deletedSessions);

        // 6. Cleanup revoked calendar links. Until then an old link answers with a notice that it is
        // off; after it, with 404, and a calendar app that still polls it gives up on its own.
        var calendarFeedRetentionDate = now.AddDays(-RevokedCalendarFeedRetentionDays);
        var deletedCalendarFeeds = await dbContext.CalendarFeeds
            .Where(feed => feed.RevokedAtUtc.HasValue && feed.RevokedAtUtc < calendarFeedRetentionDate)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCalendarFeeds > 0)
            _logger.LogInformation("Cleaned up {Count} revoked CalendarFeeds.", deletedCalendarFeeds);

        // 7. Cleanup the kurin's journal (> 365 days), a batch at a time.
        var deletedJournalEntries = await DeleteExpiredJournalAsync(
            dbContext, now.AddDays(-JournalRetentionDays), JournalDeleteBatchSize, cancellationToken);

        if (deletedJournalEntries > 0)
            _logger.LogInformation("Cleaned up {Count} old KurinAuditEvents.", deletedJournalEntries);
    }

    /// <summary>Deletes journal entries older than <paramref name="cutoffUtc"/>, <paramref name="batchSize"/> per statement.</summary>
    internal static async Task<int> DeleteExpiredJournalAsync(
        AppDbContext dbContext,
        DateTime cutoffUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var total = 0;
        int deleted;
        do
        {
            deleted = await dbContext.KurinAuditEvents
                .Where(entry => entry.OccurredAtUtc < cutoffUtc)
                .Take(batchSize)
                .ExecuteDeleteAsync(cancellationToken);
            total += deleted;
        }
        while (deleted == batchSize);

        return total;
    }

    /// <summary>
    /// Queue entries nobody is still waiting on. Two things make this narrower than "old and in a
    /// terminal status", and both were learned the hard way:
    /// <list type="bullet">
    /// <item><description><c>ApprovedForInvitation</c> is not terminal. Every invited person sits
    /// there for good — activation never moves them off it — so age alone said nothing about
    /// whether they were done.</description></item>
    /// <item><description>Deleting an entry deletes the invitations hanging off it, live ones
    /// included: the foreign key cascades in the database, and <c>ExecuteDelete</c> goes straight
    /// there. Accounts still waiting to activate lost their way in, thirty days after approval,
    /// with nothing left to resend and no record of them in the panel.</description></item>
    /// </list>
    /// So an approved entry survives for as long as an account is still waiting on it.
    /// </summary>
    internal static IQueryable<WaitlistEntry> SpentWaitlistEntries(AppDbContext dbContext, DateTime retentionDate)
        => dbContext.WaitlistEntries.Where(entry =>
            (entry.VerificationStatus == WaitlistVerificationStatus.Rejected
                && entry.ReviewedAtUtc < retentionDate)
            || (entry.VerificationStatus == WaitlistVerificationStatus.ApprovedForInvitation
                && entry.ApprovedAtUtc < retentionDate
                && !dbContext.Users.Any(user =>
                    user.Email == entry.Email
                    && user.OnboardingStatus == OnboardingStatus.PendingActivation)));
}
