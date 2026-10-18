using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Entities.InfrastructureModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Entities.KurinModule.Planning;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.Infrastructure.DbContexts;

public class AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    public DbSet<Krai> Krais { get; set; }
    public DbSet<KraiProfileRevision> KraiProfileRevisions { get; set; }
    public DbSet<Kurin> Kurins { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<Member> Members { get; set; }
    public DbSet<Membership> Memberships { get; set; }
    public DbSet<PlastLevelHistory> PlastLevelHistories { get; set; }
    public DbSet<Leadership> Leaderships { get; set; }
    public DbSet<LeadershipHistory> LeadershipHistories { get; set; }
    public DbSet<PlanningSession> PlanningSessions { get; set; }
    public DbSet<PlanningParticipant> PlanningParticipants { get; set; }
    public DbSet<ParticipantBusyRange> ParticipantBusyRanges { get; set; }
    public DbSet<AgendaItem> AgendaItems { get; set; }
    public DbSet<AgendaAssignment> AgendaAssignments { get; set; }
    public DbSet<AgendaAssignmentProgress> AgendaAssignmentProgress { get; set; }
    public DbSet<AgendaCategory> AgendaCategories { get; set; }
    public DbSet<AgendaResponse> AgendaResponses { get; set; }
    public DbSet<BadgeProgress> BadgeProgresses { get; set; }
    public DbSet<BadgeProgressAuditEvent> BadgeProgressAuditEvents { get; set; }
    public DbSet<ProbeProgress> ProbeProgresses { get; set; }
    public DbSet<ProbeProgressAuditEvent> ProbeProgressAuditEvents { get; set; }
    public DbSet<ProbePointProgress> ProbePointProgresses { get; set; }
    public DbSet<MentorAssignment> MentorAssignments { get; set; }
    public DbSet<MemberWarning> MemberWarnings { get; set; }
    public DbSet<MemberAward> MemberAwards { get; set; }

    public DbSet<KurinDuesRate> KurinDuesRates { get; set; }
    public DbSet<GroupDuesRate> GroupDuesRates { get; set; }
    public DbSet<DuesConcession> DuesConcessions { get; set; }
    public DbSet<DuesCharge> DuesCharges { get; set; }
    public DbSet<DuesEntry> DuesEntries { get; set; }
    public DbSet<DuesEntryEvent> DuesEntryEvents { get; set; }

    public DbSet<KurinScoreSettings> KurinScoreSettings { get; set; }
    public DbSet<ScoreRule> ScoreRules { get; set; }
    public DbSet<ScoreAttendanceRate> ScoreAttendanceRates { get; set; }
    public DbSet<ScoreItem> ScoreItems { get; set; }
    public DbSet<ScoreStage> ScoreStages { get; set; }
    public DbSet<ScoreAttendance> ScoreAttendances { get; set; }
    public DbSet<ScoreEntry> ScoreEntries { get; set; }
    public DbSet<ScoreGroupMove> ScoreGroupMoves { get; set; }
    public DbSet<ScoreTrailEvent> ScoreTrailEvents { get; set; }
    public DbSet<PrivateScoreCriterion> PrivateScoreCriteria { get; set; }
    public DbSet<PrivateScoreEntry> PrivateScoreEntries { get; set; }

    public DbSet<WaitlistEntry> WaitlistEntries { get; set; }
    public DbSet<UserRefreshToken> UserRefreshTokens { get; set; }
    public DbSet<Invitation> Invitations { get; set; }
    public DbSet<AppNotification> AppNotifications { get; set; }
    public DbSet<WebPushSubscription> WebPushSubscriptions { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }
    public DbSet<UserTileLayout> UserTileLayouts { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Stamps a new member's public code before it is written. It sits here rather than in a use
    /// case because a member is also opened by the seeders and by account activation, and a person
    /// without a code cannot be found by the one thing another kurin can ask for.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Member>())
        {
            if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.PublicId))
            {
                entry.Entity.PublicId = MemberPublicId.For(entry.Entity.MemberKey);
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // The two краї every instance starts with. Their first profile revision is empty: a
        // section a profile leaves out takes its value from code, which is how Lilejka works today.
        var seededAt = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc);
        builder.Entity<Krai>(entity =>
        {
            entity.HasKey(e => e.Code);
            entity.HasData(
                new Krai { Code = Krai.Ukraine, Name = "Україна", CreatedAtUtc = seededAt },
                new Krai { Code = Krai.Czechia, Name = "Чехія", CreatedAtUtc = seededAt });
        });

        builder.Entity<KraiProfileRevision>(entity =>
        {
            entity.HasKey(e => e.KraiProfileRevisionKey);
            entity.HasIndex(e => new { e.KraiCode, e.Revision }).IsUnique();
            entity.Property(e => e.KraiCode).HasMaxLength(8);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.HasOne(e => e.Krai)
                  .WithMany(k => k.ProfileRevisions)
                  .HasForeignKey(e => e.KraiCode)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasData(
                new KraiProfileRevision
                {
                    KraiProfileRevisionKey = new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a01"),
                    KraiCode = Krai.Ukraine,
                    Revision = 1,
                    SchemaVersion = 1,
                    Json = "{}",
                    CreatedAtUtc = seededAt,
                    Note = "Початковий профіль"
                },
                new KraiProfileRevision
                {
                    KraiProfileRevisionKey = new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a02"),
                    KraiCode = Krai.Czechia,
                    Revision = 1,
                    SchemaVersion = 1,
                    Json = "{}",
                    CreatedAtUtc = seededAt,
                    Note = "Початковий профіль"
                });
        });

        builder.Entity<Kurin>(entity =>
        {
            entity.Property(e => e.Branch).HasConversion<int>();
            entity.HasKey(e => e.KurinKey);
            entity.Property(e => e.KraiCode)
                .HasMaxLength(8)
                .HasDefaultValue(Krai.Ukraine);
            entity.HasOne<Krai>()
                  .WithMany()
                  .HasForeignKey(e => e.KraiCode)
                  .OnDelete(DeleteBehavior.Restrict);
            // A number is the kurin's within its край: two краї can each have a kurin 12.
            entity.HasIndex(e => new { e.KraiCode, e.Number }).IsUnique();
            entity.Property(e => e.Stanytsia)
                .HasMaxLength(120);
            entity.Property(e => e.RegionOrCountry)
                .HasMaxLength(120);
            entity.Property(e => e.NamedAfter)
                .HasMaxLength(200);
            entity.Property(e => e.Description)
                .HasMaxLength(4000);
        });

        builder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.GroupKey);
            entity.Property(e => e.Description)
                .HasMaxLength(1000);
            entity.Property(e => e.SilhouetteBlobName)
                .HasMaxLength(500);
            entity.HasOne(e => e.Kurin)
                  .WithMany(k => k.Groups)
                  .HasForeignKey(e => e.KurinKey)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Member>(entity =>
        {
            entity.HasKey(e => e.MemberKey);
            entity.Property(e => e.PublicId)
                  .HasMaxLength(20)
                  .IsRequired();
            entity.HasIndex(e => e.PublicId)
                  .IsUnique();
            entity.HasOne(entity => entity.User)
                  .WithOne()
                  .HasForeignKey<Member>(e => e.UserKey)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Membership>(entity =>
        {
            entity.HasKey(e => e.MembershipKey);
            entity.Property(e => e.Kind).HasConversion<int>();
            entity.HasOne(e => e.Kurin)
                  .WithMany(k => k.Memberships)
                  .HasForeignKey(e => e.KurinKey)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Group)
                  .WithMany()
                  .HasForeignKey(e => e.GroupKey)
                  .OnDelete(DeleteBehavior.NoAction);

            // The reads this table exists for: everyone in a kurin, every kurin of one person,
            // and the scope of the account making a request.
            entity.HasIndex(e => new { e.KurinKey, e.LeftAtUtc });
            entity.HasIndex(e => new { e.MemberKey, e.LeftAtUtc });
            entity.HasIndex(e => new { e.UserKey, e.LeftAtUtc });

            // A person belongs to a kurin once at a time. Past memberships are excluded, so
            // rejoining after leaving is allowed and being in it twice at once is not.
            entity.HasIndex(e => new { e.MemberKey, e.KurinKey })
                  .IsUnique()
                  .HasFilter("[LeftAtUtc] IS NULL");
        });

        builder.Entity<MemberWarning>(entity =>
        {
            entity.HasIndex(e => new { e.KurinKey, e.RevokedAtUtc });
            entity.HasKey(e => e.MemberWarningKey);
            entity.Property(e => e.Level)
                .HasConversion<int>();
            entity.HasIndex(e => new { e.MemberKey, e.Level });
            entity.HasIndex(e => e.ExpiresAtUtc);
            entity.HasOne(e => e.Member)
                  .WithMany(m => m.MemberWarnings)
                  .HasForeignKey(e => e.MemberKey)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Dues module. Money is decimal(18,2) everywhere; quarters are DuesQuarter.Index. Kurins, гуртки
        // and memberships are held by key with no foreign key: money outlives a deleted гурток.
        builder.Entity<KurinDuesRate>(entity =>
        {
            entity.HasKey(e => e.KurinDuesRateKey);
            entity.Property(e => e.StanytsiaFull).HasPrecision(18, 2);
            entity.Property(e => e.StanytsiaReduced).HasPrecision(18, 2);
            entity.Property(e => e.KurinShare).HasPrecision(18, 2);
            entity.Property(e => e.KurinReduced).HasPrecision(18, 2);
            entity.Property(e => e.KraiFull).HasPrecision(18, 2);
            entity.Property(e => e.KraiReduced).HasPrecision(18, 2);
            // A plain JSON column rather than an owned collection: rates are read untracked and saved
            // back with Update, which owned collections cannot do.
            entity.Property(e => e.Articles)
                  .HasConversion(
                      a => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null),
                      json => JsonSerializer.Deserialize<List<KurinDuesArticle>>(json, (JsonSerializerOptions?)null) ?? new List<KurinDuesArticle>(),
                      new ValueComparer<List<KurinDuesArticle>>(
                          (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                          a => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null).GetHashCode(),
                          a => JsonSerializer.Deserialize<List<KurinDuesArticle>>(JsonSerializer.Serialize(a, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));
            entity.HasIndex(e => new { e.KurinKey, e.FromQuarter }).IsUnique();
        });

        builder.Entity<GroupDuesRate>(entity =>
        {
            entity.HasKey(e => e.GroupDuesRateKey);
            entity.Property(e => e.GroupShare).HasPrecision(18, 2);
            entity.HasIndex(e => new { e.GroupKey, e.FromQuarter }).IsUnique();
            entity.HasIndex(e => e.KurinKey);
        });

        builder.Entity<DuesConcession>(entity =>
        {
            entity.HasKey(e => e.DuesConcessionKey);
            entity.HasIndex(e => new { e.MembershipKey, e.FromQuarter }).IsUnique();
            entity.HasIndex(e => e.KurinKey);
        });

        builder.Entity<DuesCharge>(entity =>
        {
            entity.HasKey(e => e.DuesChargeKey);
            entity.HasIndex(e => new { e.MembershipKey, e.Quarter }).IsUnique();
            entity.HasIndex(e => e.KurinKey);
        });

        builder.Entity<DuesEntry>(entity =>
        {
            entity.HasKey(e => e.DuesEntryKey);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Kind).HasConversion<int>();
            entity.Property(e => e.Method).HasConversion<int>();
            entity.Property(e => e.CounterMethod).HasConversion<int?>();
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Ignore(e => e.IsVerified);
            entity.Ignore(e => e.IsDeleted);
            entity.HasIndex(e => new { e.KurinKey, e.GroupKey });
            entity.HasIndex(e => e.MembershipKey);
        });

        builder.Entity<DuesEntryEvent>(entity =>
        {
            entity.HasKey(e => e.DuesEntryEventKey);
            entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
            entity.HasOne(e => e.DuesEntry)
                .WithMany(e => e.Events)
                .HasForeignKey(e => e.DuesEntryKey)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Score module. Kurins, гуртки, memberships and events are held by key with no foreign key, as
        // in dues. The two "only once" rules of точкування are unique indexes over the rows still
        // standing, so they hold whoever writes and however fast.
        builder.Entity<KurinScoreSettings>(entity =>
        {
            entity.HasKey(e => e.KurinScoreSettingsKey);
            entity.Property(e => e.Algorithm).HasConversion<int>();
            entity.HasIndex(e => e.KurinKey).IsUnique();
        });

        builder.Entity<ScoreRule>(entity =>
        {
            entity.HasKey(e => e.ScoreRuleKey);
            entity.Property(e => e.Source).HasConversion<int>();
            entity.HasIndex(e => new { e.KurinKey, e.Source, e.Variant, e.FromDate }).IsUnique();
        });

        builder.Entity<ScoreAttendanceRate>(entity =>
        {
            entity.HasKey(e => e.ScoreAttendanceRateKey);
            entity.HasIndex(e => e.KurinKey);
            entity.HasIndex(e => e.AgendaCategoryKey).IsUnique().HasFilter("[AgendaCategoryKey] IS NOT NULL");
            entity.HasIndex(e => e.AgendaItemKey).IsUnique().HasFilter("[AgendaItemKey] IS NOT NULL");
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_ScoreAttendanceRates_OneTarget",
                "([AgendaCategoryKey] IS NULL AND [AgendaItemKey] IS NOT NULL) OR ([AgendaCategoryKey] IS NOT NULL AND [AgendaItemKey] IS NULL)"));
        });

        builder.Entity<ScoreItem>(entity =>
        {
            entity.HasKey(e => e.ScoreItemKey);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.KurinKey);
        });

        builder.Entity<ScoreStage>(entity =>
        {
            entity.HasKey(e => e.ScoreStageKey);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.KurinKey);
        });

        builder.Entity<ScoreAttendance>(entity =>
        {
            entity.HasKey(e => e.ScoreAttendanceKey);
            entity.Ignore(e => e.IsRemoved);
            entity.HasIndex(e => new { e.MembershipKey, e.AgendaItemKey, e.OccurrenceStartUtc })
                .IsUnique()
                .HasFilter("[RemovedAtUtc] IS NULL")
                .HasDatabaseName("IX_ScoreAttendances_OnePerOccurrence");
            entity.HasIndex(e => new { e.KurinKey, e.AgendaItemKey });
        });

        builder.Entity<ScoreEntry>(entity =>
        {
            entity.HasKey(e => e.ScoreEntryKey);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Ignore(e => e.IsDeleted);
            entity.HasIndex(e => new { e.MembershipKey, e.AgendaItemKey, e.OccurrenceStartUtc, e.ScoreItemKey })
                .IsUnique()
                .HasFilter("[MembershipKey] IS NOT NULL AND [AgendaItemKey] IS NOT NULL AND [ScoreItemKey] IS NOT NULL AND [DeletedAtUtc] IS NULL")
                .HasDatabaseName("IX_ScoreEntries_ItemOncePerPerson");
            entity.HasIndex(e => new { e.GroupKey, e.AgendaItemKey, e.OccurrenceStartUtc, e.ScoreItemKey })
                .IsUnique()
                .HasFilter("[GroupKey] IS NOT NULL AND [AgendaItemKey] IS NOT NULL AND [ScoreItemKey] IS NOT NULL AND [DeletedAtUtc] IS NULL")
                .HasDatabaseName("IX_ScoreEntries_ItemOncePerGroup");
            entity.HasIndex(e => e.KurinKey);
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_ScoreEntries_OneTarget",
                "([MembershipKey] IS NULL AND [GroupKey] IS NOT NULL) OR ([MembershipKey] IS NOT NULL AND [GroupKey] IS NULL)"));
        });

        builder.Entity<ScoreGroupMove>(entity =>
        {
            entity.HasKey(e => e.ScoreGroupMoveKey);
            entity.HasIndex(e => e.KurinKey);
            entity.HasIndex(e => e.MembershipKey);
        });

        // The КВ's private score: its own two tables, so nothing of it can leak into a public read by
        // a missed filter — the public ledger simply never loads them.
        builder.Entity<PrivateScoreCriterion>(entity =>
        {
            entity.HasKey(e => e.PrivateScoreCriterionKey);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.KurinKey);
        });

        builder.Entity<PrivateScoreEntry>(entity =>
        {
            entity.HasKey(e => e.PrivateScoreEntryKey);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Ignore(e => e.IsDeleted);
            entity.HasIndex(e => e.KurinKey);
            entity.HasIndex(e => e.MembershipKey);
        });

        builder.Entity<ScoreTrailEvent>(entity =>
        {
            entity.HasKey(e => e.ScoreTrailEventKey);
            entity.Property(e => e.Subject).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.SubjectKey);
            entity.HasIndex(e => e.KurinKey);
        });

        builder.Entity<MemberAward>(entity =>
        {
            entity.HasKey(e => e.MemberAwardKey);
            entity.Property(e => e.Level)
                .HasConversion<int>();
            entity.Property(e => e.Status)
                .HasConversion<int>();
            entity.HasIndex(e => new { e.MemberKey, e.Level });
            entity.HasOne(e => e.Member)
                  .WithMany(m => m.MemberAwards)
                  .HasForeignKey(e => e.MemberKey)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PlastLevelHistory>(entity =>
        {
            entity.HasKey(e => e.PlastLevelHistoryKey);
            entity.HasOne(e => e.Member)
                  .WithMany(m => m.PlastLevelHistory)
                  .HasForeignKey(e => e.MemberKey)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.PlastLevel)
                  .HasConversion<int>();
        });

        builder.Entity<Leadership>(entity =>
        {
            entity.HasKey(e => e.LeadershipKey);
            entity.Property(e => e.Type)
                  .HasConversion<int>();
            entity.HasIndex(e => new { e.Type, e.KurinKey, e.GroupKey });
            entity.HasOne(e => e.Kurin)
                .WithMany(k => k.Leaderships)
                .HasForeignKey(e => e.KurinKey)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Group)
                .WithOne(g => g.Leadership)
                .HasForeignKey<Leadership>(e => e.GroupKey)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LeadershipHistory>(entity =>
        {
            entity.HasKey(e => e.LeadershipHistoryKey);
            entity.HasOne(e => e.Member)
                  .WithMany(m => m.LeadershipHistories)
                  .HasForeignKey(e => e.MemberKey)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Leadership)
                  .WithMany(l => l.LeadershipHistories)
                  .HasForeignKey(e => e.LeadershipKey)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.LeadershipKey, e.Role, e.StartDate });
            entity.HasIndex(e => new { e.MemberKey, e.StartDate });
        });

        builder.Entity<PlanningSession>(entity =>
        {
            entity.HasKey(e => e.PlanningSessionKey);
            entity.HasOne(e => e.Kurin)
                  .WithMany(k => k.PlanningSessions)
                  .HasForeignKey(e => e.KurinKey)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PlanningParticipant>(entity =>
        {
            entity.HasKey(e => e.PlanningParticipantKey);
            entity.HasOne(e => e.PlanningSession)
                  .WithMany(ps => ps.Participants)
                  .HasForeignKey(e => e.PlanningSessionKey)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ParticipantBusyRange>(entity =>
        {
            entity.HasKey(e => e.ParticipantBusyRangeKey);
            entity.HasOne(e => e.PlanningParticipant)
                  .WithMany(pp => pp.BusyRanges)
                  .HasForeignKey(e => e.PlanningParticipantKey)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AgendaItem>(entity =>
        {
            entity.HasKey(e => e.AgendaItemKey);
            entity.Property(e => e.Kind).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.RecurrenceFrequency).HasConversion<int>();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Location).HasMaxLength(200);
            entity.HasOne(e => e.Kurin)
                  .WithMany(k => k.AgendaItems)
                  .HasForeignKey(e => e.KurinKey)
                  .OnDelete(DeleteBehavior.Cascade);
            // The calendar queries by kurin and date window, so index both.
            entity.HasIndex(e => new { e.KurinKey, e.StartUtc });
            // Every feed filters active vs archived, and the nightly sweep looks for old archived rows.
            entity.HasIndex(e => new { e.KurinKey, e.ArchivedAtUtc });
            // NoAction (not SetNull) so Kurin keeps a single cascade path to AgendaItems: Category→Kurin
            // is Cascade, and a second Kurin→Category→item(SetNull) path would trip SQL Server 1785.
            // DeleteAgendaCategoryCommand nulls out referencing items itself before removing the group.
            entity.HasOne(e => e.Category)
                  .WithMany()
                  .HasForeignKey(e => e.AgendaCategoryKey)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<AgendaCategory>(entity =>
        {
            entity.HasKey(e => e.AgendaCategoryKey);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ColorHex).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Icon).HasMaxLength(64);
            entity.Property(e => e.DefaultDescription).HasMaxLength(2000);
            // Cascade: an event group belongs to its kurin and dies with it (so the seeder's kurin reset
            // and any kurin delete clear categories automatically). The item→category side is NoAction to
            // keep this the only cascade path to AgendaItems.
            entity.HasOne(e => e.Kurin)
                  .WithMany()
                  .HasForeignKey(e => e.KurinKey)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.KurinKey, e.IsArchived });
        });

        builder.Entity<AgendaResponse>(entity =>
        {
            entity.HasKey(e => e.AgendaResponseKey);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasOne(e => e.AgendaItem)
                  .WithMany(a => a.Responses)
                  .HasForeignKey(e => e.AgendaItemKey)
                  .OnDelete(DeleteBehavior.Cascade);
            // One answer per user per occurrence, NULL being the one-off event itself. SQL Server treats
            // NULLs as equal in a unique index, which is exactly the one-row-per-user rule a one-off
            // needs — but EF would add a «IS NOT NULL» filter by default and exempt those rows, so the
            // filter is switched off. The RSVP list also queries by item and occurrence.
            entity.HasIndex(e => new { e.AgendaItemKey, e.UserKey, e.OccurrenceStartUtc }).IsUnique().HasFilter(null);
        });

        builder.Entity<AgendaAssignment>(entity =>
        {
            entity.HasKey(e => e.AgendaAssignmentKey);
            entity.Property(e => e.TargetType).HasConversion<int>();
            entity.HasOne(e => e.AgendaItem)
                  .WithMany(a => a.Assignments)
                  .HasForeignKey(e => e.AgendaItemKey)
                  .OnDelete(DeleteBehavior.Cascade);
            // One target appears once per item; also the lookup path for "what is assigned to me".
            entity.Property(e => e.CompletionMode).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasIndex(e => new { e.AgendaItemKey, e.TargetType, e.TargetKey }).IsUnique();
            entity.HasIndex(e => new { e.TargetType, e.TargetKey });
        });

        builder.Entity<AgendaAssignmentProgress>(entity =>
        {
            entity.HasKey(e => e.AgendaAssignmentProgressKey);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasOne(e => e.Assignment)
                  .WithMany(a => a.Progress)
                  .HasForeignKey(e => e.AgendaAssignmentKey)
                  .OnDelete(DeleteBehavior.Cascade);
            // One part per person per target; a second move overwrites the row.
            entity.HasIndex(e => new { e.AgendaAssignmentKey, e.MemberKey }).IsUnique();
        });

        builder.Entity<BadgeProgress>(entity =>
        {
            entity.HasKey(e => e.BadgeProgressKey);
            entity.Property(e => e.BadgeId)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(e => e.Status)
                .HasConversion<int>();
            entity.HasIndex(e => new { e.MemberKey, e.BadgeId })
                .IsUnique();
        });

        builder.Entity<BadgeProgressAuditEvent>(entity =>
        {
            entity.HasKey(e => e.BadgeProgressAuditEventKey);
            entity.Property(e => e.FromStatus)
                .HasConversion<int?>();
            entity.Property(e => e.ToStatus)
                .HasConversion<int>();
            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(e => e.ActorRole)
                .HasMaxLength(50)
                .IsRequired();
            entity.HasOne(e => e.BadgeProgress)
                .WithMany(p => p.AuditEvents)
                .HasForeignKey(e => e.BadgeProgressKey)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.BadgeProgressKey);
        });

        builder.Entity<ProbeProgress>(entity =>
        {
            entity.HasKey(e => e.ProbeProgressKey);
            entity.Property(e => e.ProbeId)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(e => e.Status)
                .HasConversion<int>();
            entity.HasIndex(e => new { e.MemberKey, e.ProbeId })
                .IsUnique();
        });

        builder.Entity<ProbeProgressAuditEvent>(entity =>
        {
            entity.HasKey(e => e.ProbeProgressAuditEventKey);
            entity.Property(e => e.FromStatus)
                .HasConversion<int?>();
            entity.Property(e => e.ToStatus)
                .HasConversion<int>();
            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(e => e.ActorRole)
                .HasMaxLength(50)
                .IsRequired();
            entity.HasOne(e => e.ProbeProgress)
                .WithMany(p => p.AuditEvents)
                .HasForeignKey(e => e.ProbeProgressKey)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.ProbeProgressKey);
        });

        builder.Entity<ProbePointProgress>(entity =>
        {
            entity.HasKey(e => e.ProbePointProgressKey);
            entity.Property(e => e.ProbeId)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(e => e.PointId)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(e => e.SignedByRole)
                .HasMaxLength(50);
            entity.HasIndex(e => new { e.MemberKey, e.ProbeId, e.PointId })
                .IsUnique();
        });

        builder.Entity<MentorAssignment>(entity =>
        {
            entity.HasKey(e => e.MentorAssignmentKey);
            entity.HasOne(e => e.Group)
                .WithMany(g => g.MentorAssignments)
                .HasForeignKey(e => e.GroupKey)
                .OnDelete(DeleteBehavior.Cascade);
            // Revoking keeps the row as history, so the pair may repeat: one active assignment at a
            // time, any number of revoked ones. Unfiltered, re-assigning a mentor to a group they
            // had been removed from failed on this index with a 500.
            entity.HasIndex(e => new { e.MentorUserKey, e.GroupKey })
                .IsUnique()
                .HasFilter("[RevokedAtUtc] IS NULL");
            entity.HasIndex(e => e.MentorUserKey)
                .HasFilter("[RevokedAtUtc] IS NULL");
        });

        builder.Entity<UserRefreshToken>(entity =>
        {
            entity.HasKey(e => e.UserRefreshTokenKey);
            entity.Property(e => e.Token)
                .HasMaxLength(512)
                .IsRequired();
            // Looked up by token on every refresh, and swept per user on a password change.
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.RevokedAtUtc });
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WaitlistEntry>(entity =>
        {
            entity.HasKey(e => e.WaitlistEntryKey);
            entity.Property(e => e.Stanytsia)
                .HasMaxLength(120);
            entity.Property(e => e.RegionOrCountry)
                .HasMaxLength(120);
            entity.Property(e => e.VerificationStatus)
                .HasConversion<string>();
            entity.Property(e => e.Source)
                .HasConversion<string>();
            entity.HasIndex(e => e.Email).IsUnique();
            // The kurin a founder's approval opened; it goes with the kurin, not with the entry.
            entity.HasOne<Kurin>()
                  .WithMany()
                  .HasForeignKey(e => e.FoundedKurinKey)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // The kurin an account stepped into. With the database forgetting it alongside the kurin,
        // no new path of deletion can leave an account scoped to a kurin that is gone (STAB-05).
        builder.Entity<AppUser>()
            .HasOne<Kurin>()
            .WithMany()
            .HasForeignKey(user => user.ActiveKurinKey)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Invitation>(entity =>
        {
            entity.HasKey(e => e.InvitationKey);
            entity.HasOne(e => e.WaitlistEntry)
                .WithMany()
                .HasForeignKey(e => e.WaitlistEntryKey)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.TargetUser)
                .WithMany()
                .HasForeignKey(e => e.TargetUserKey)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.Token).IsUnique();
        });

        builder.Entity<AppNotification>(entity =>
        {
            entity.HasKey(e => e.NotificationKey);
            entity.Property(e => e.Type)
                .HasConversion<int>();
            entity.Property(e => e.Severity)
                .HasConversion<int>();
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(e => e.Body)
                .HasMaxLength(1000)
                .IsRequired();
            entity.Property(e => e.EntityType)
                .HasMaxLength(100);
            entity.Property(e => e.Route)
                .HasMaxLength(1000);
            entity.Property(e => e.PayloadJson)
                .HasMaxLength(2000);
            entity.Property(e => e.DeduplicationKey)
                .HasMaxLength(300);
            entity.HasIndex(e => new { e.RecipientUserKey, e.CreatedAtUtc });
            entity.HasIndex(e => new { e.RecipientUserKey, e.ReadAtUtc });
            entity.HasIndex(e => new { e.RecipientUserKey, e.DeduplicationKey })
                .HasFilter("[DeduplicationKey] IS NOT NULL AND [ReadAtUtc] IS NULL");
        });

        // A device's push endpoint. Unique, so a device that signs in as someone else moves its row;
        // the account's rows go with the account.
        builder.Entity<WebPushSubscription>(entity =>
        {
            entity.HasKey(e => e.WebPushSubscriptionKey);
            entity.Property(e => e.Endpoint)
                .HasMaxLength(800)
                .IsRequired();
            entity.Property(e => e.P256dh)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(e => e.Auth)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(e => e.UserAgent)
                .HasMaxLength(300);
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(e => e.UserKey)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.Endpoint).IsUnique();
        });

        builder.Entity<UserTileLayout>(entity =>
        {
            entity.HasKey(e => e.UserTileLayoutKey);
            entity.Property(e => e.BoardKey)
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(e => e.TileOrderJson)
                .HasMaxLength(2000)
                .IsRequired();
            entity.Property(e => e.HiddenTilesJson)
                .HasMaxLength(2000)
                .IsRequired()
                .HasDefaultValue("[]");
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserKey)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserKey, e.BoardKey }).IsUnique();
        });
    }
}
