using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Infrastructure.BackgroundServices;
using ProjectK.Infrastructure.DbContexts;
using Xunit;

namespace ProjectK.API.Tests.Services;

public class AuditCleanupBackgroundServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AuditCleanupBackgroundServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Close();
    }

    [Fact]
    public async Task CleanupOldRecordsAsync_ShouldRemoveOldRecordsAndKeepNewOnes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(_connection));
        var serviceProvider = services.BuildServiceProvider();

        var loggerMock = new Mock<ILogger<AuditCleanupBackgroundService>>();
        var service = new AuditCleanupBackgroundService(serviceProvider, loggerMock.Object);

        var oldDateOnboarding = DateTime.UtcNow.AddDays(-31);
        var newDateOnboarding = DateTime.UtcNow.AddDays(-29);
        var oldReadNotificationDate = DateTime.UtcNow.AddDays(-8);
        var newReadNotificationDate = DateTime.UtcNow.AddDays(-6);
        var oldUnreadNotificationDate = DateTime.UtcNow.AddDays(-31);
        var newUnreadNotificationDate = DateTime.UtcNow.AddDays(-29);

        using (var context = new AppDbContext(_options))
        {
            context.WaitlistEntries.AddRange(
                new WaitlistEntry
                {
                    WaitlistEntryKey = Guid.NewGuid(),
                    FirstName = "Old",
                    LastName = "User",
                    Email = "old@example.com",
                    PhoneNumber = "1234567890",
                    DateOfBirth = oldDateOnboarding,
                    VerificationStatus = WaitlistVerificationStatus.Rejected,
                    ReviewedAtUtc = oldDateOnboarding
                },
                new WaitlistEntry
                {
                    WaitlistEntryKey = Guid.NewGuid(),
                    FirstName = "New",
                    LastName = "User",
                    Email = "new@example.com",
                    PhoneNumber = "0987654321",
                    DateOfBirth = newDateOnboarding,
                    VerificationStatus = WaitlistVerificationStatus.Rejected,
                    ReviewedAtUtc = newDateOnboarding
                }
            );

            context.Invitations.AddRange(
                new Invitation { InvitationKey = Guid.NewGuid(), Token = "old", ExpiresAtUtc = oldDateOnboarding },
                new Invitation { InvitationKey = Guid.NewGuid(), Token = "new", ExpiresAtUtc = newDateOnboarding }
            );

            context.AppNotifications.AddRange(
                CreateNotification("old-read", oldReadNotificationDate, oldReadNotificationDate),
                CreateNotification("new-read", oldReadNotificationDate, newReadNotificationDate),
                CreateNotification("old-unread", oldUnreadNotificationDate, null),
                CreateNotification("new-unread", newUnreadNotificationDate, null)
            );

            await context.SaveChangesAsync();
        }

        // Act
        // We use reflection to call the private method for testing, or we just run the service briefly.
        // Since ExecuteAsync is protected and loops, we can expose the logic or use a reflection workaround.
        var method = typeof(AuditCleanupBackgroundService).GetMethod("CleanupOldRecordsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Assert
        using (var context = new AppDbContext(_options))
        {
            Assert.Equal(1, await context.WaitlistEntries.CountAsync());
            Assert.Equal(1, await context.Invitations.CountAsync());

            var remainingNotifications = await context.AppNotifications.ToListAsync();
            Assert.Equal(2, remainingNotifications.Count);
            Assert.Contains(remainingNotifications, n => n.Title == "new-read");
            Assert.Contains(remainingNotifications, n => n.Title == "new-unread");
        }
    }

    /// <summary>
    /// The rule that cost real people their way in: an approved entry is not a finished one.
    /// Everyone who was ever invited stays at <c>ApprovedForInvitation</c>, so deleting on age
    /// alone took the entries of accounts still waiting to activate — and, because the key
    /// cascades, the invitations hanging off them. An entry whose account has finished is still
    /// swept up; an entry whose account is still waiting is not, however old.
    /// </summary>
    [Fact]
    public async Task CleanupOldRecordsAsync_KeepsApprovedEntriesOfAccountsStillWaitingToActivate()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(_connection));
        var serviceProvider = services.BuildServiceProvider();
        var service = new AuditCleanupBackgroundService(serviceProvider, new Mock<ILogger<AuditCleanupBackgroundService>>().Object);

        var longAgo = DateTime.UtcNow.AddDays(-31);
        var waiting = CreateApprovedEntry("waiting@example.com", longAgo);
        var finished = CreateApprovedEntry("finished@example.com", longAgo);

        using (var context = new AppDbContext(_options))
        {
            context.WaitlistEntries.AddRange(waiting, finished);
            context.Users.AddRange(
                CreateUser("waiting@example.com", OnboardingStatus.PendingActivation),
                CreateUser("finished@example.com", OnboardingStatus.Active));

            context.Invitations.Add(new Invitation
            {
                InvitationKey = Guid.NewGuid(),
                Token = "live",
                WaitlistEntryKey = waiting.WaitlistEntryKey,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
            });

            await context.SaveChangesAsync();
        }

        var method = typeof(AuditCleanupBackgroundService).GetMethod("CleanupOldRecordsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        using (var context = new AppDbContext(_options))
        {
            var remaining = await context.WaitlistEntries.Select(entry => entry.Email).ToListAsync();
            Assert.Contains("waiting@example.com", remaining);
            Assert.DoesNotContain("finished@example.com", remaining);
            Assert.Equal(1, await context.Invitations.CountAsync(invitation => invitation.Token == "live"));
        }
    }

    private static WaitlistEntry CreateApprovedEntry(string email, DateTime approvedAtUtc) => new()
    {
        WaitlistEntryKey = Guid.NewGuid(),
        FirstName = "Approved",
        LastName = "Person",
        Email = email,
        PhoneNumber = "1234567890",
        DateOfBirth = DateTime.UnixEpoch,
        VerificationStatus = WaitlistVerificationStatus.ApprovedForInvitation,
        ReviewedAtUtc = approvedAtUtc,
        ApprovedAtUtc = approvedAtUtc
    };

    private static AppUser CreateUser(string email, OnboardingStatus status) => new()
    {
        Id = Guid.NewGuid(),
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        FirstName = "Approved",
        LastName = "Person",
        OnboardingStatus = status
    };

    private static AppNotification CreateNotification(string title, DateTime createdAtUtc, DateTime? readAtUtc)
    {
        return new AppNotification
        {
            NotificationKey = Guid.NewGuid(),
            RecipientUserKey = Guid.NewGuid(),
            Type = AppNotificationType.MemberProfileVerified,
            Severity = AppNotificationSeverity.Info,
            Title = title,
            Body = title,
            CreatedAtUtc = createdAtUtc,
            ReadAtUtc = readAtUtc
        };
    }
}
